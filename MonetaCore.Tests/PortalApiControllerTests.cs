using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.ApiModels;
using MonetaCore.Controllers.Api;
using MonetaCore.Data;
using MonetaCore.Models;

namespace MonetaCore.Tests;

public class PortalApiControllerTests
{
    [Fact]
    public async Task SubmitDispute_PersistsDisputeAndQueuesEvent()
    {
        await using var dbContext = CreateDbContext();

        var client = new ClientAccount
        {
            Id = 1,
            CompanyName = "Acme Corp",
            ContactPerson = "Jane",
            Email = "jane@acme.local",
            Phone = "555-1111",
            Address = "Manila"
        };

        var invoice = new Invoice
        {
            Id = 100,
            InvoiceNumber = "INV-100",
            ClientAccountId = 1,
            CreatedByUserId = 1,
            Status = DomainValues.InvoiceStatus.Issued,
            Subtotal = 100m,
            TaxRate = 0.12m,
            TaxAmount = 12m,
            TotalAmount = 112m,
            BalanceDue = 112m,
            AmountPaid = 0m
        };

        dbContext.Clients.Add(client);
        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        var currentUser = new FakeCurrentUserService
        {
            UserId = 99,
            UserName = "client@acme.local",
            Role = ApplicationRoles.Client
        };
        var audit = new FakeAuditService();
        var outbox = new FakeEventOutboxService();

        var controller = new PortalApiController(dbContext, currentUser, audit, outbox)
        {
            ControllerContext = BuildControllerContext(
                ApplicationRoles.Client,
                clientAccountId: 1,
                traceIdentifier: "portal-trace-1")
        };

        var request = new PortalDisputeCreateRequest
        {
            InvoiceId = 100,
            Subject = "Incorrect amount",
            Message = "Please verify line items."
        };

        ActionResult<PortalDisputeResponseDto> actionResult = await controller.SubmitDispute(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<PortalDisputeResponseDto>(ok.Value);

        PortalDispute? saved = await dbContext.PortalDisputes.SingleOrDefaultAsync();

        Assert.NotNull(saved);
        Assert.Equal(response.DisputeReference, saved.DisputeReference);
        Assert.Equal(DomainValues.DisputeStatus.Submitted, saved.Status);
        Assert.Single(audit.Entries);
        Assert.Single(outbox.Envelopes);
        Assert.Equal("PortalDisputeSubmitted", outbox.Envelopes[0].EventType);
    }

    [Fact]
    public async Task GetDisputes_ClientRoleReturnsOnlyOwnClientDisputes()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Clients.AddRange(
            new ClientAccount
            {
                Id = 1,
                CompanyName = "Client One",
                ContactPerson = "A",
                Email = "a@client.local",
                Phone = "1",
                Address = "Addr 1"
            },
            new ClientAccount
            {
                Id = 2,
                CompanyName = "Client Two",
                ContactPerson = "B",
                Email = "b@client.local",
                Phone = "2",
                Address = "Addr 2"
            });

        dbContext.Invoices.AddRange(
            new Invoice
            {
                Id = 201,
                InvoiceNumber = "INV-201",
                ClientAccountId = 1,
                CreatedByUserId = 1,
                Status = DomainValues.InvoiceStatus.Issued,
                Subtotal = 100m,
                TaxRate = 0m,
                TaxAmount = 0m,
                TotalAmount = 100m,
                BalanceDue = 100m
            },
            new Invoice
            {
                Id = 202,
                InvoiceNumber = "INV-202",
                ClientAccountId = 2,
                CreatedByUserId = 1,
                Status = DomainValues.InvoiceStatus.Issued,
                Subtotal = 200m,
                TaxRate = 0m,
                TaxAmount = 0m,
                TotalAmount = 200m,
                BalanceDue = 200m
            });

        dbContext.PortalDisputes.AddRange(
            new PortalDispute
            {
                DisputeReference = "DSP-ONE",
                InvoiceId = 201,
                Subject = "Question 1",
                Message = "Message 1",
                Status = DomainValues.DisputeStatus.Submitted
            },
            new PortalDispute
            {
                DisputeReference = "DSP-TWO",
                InvoiceId = 202,
                Subject = "Question 2",
                Message = "Message 2",
                Status = DomainValues.DisputeStatus.Submitted
            });

        await dbContext.SaveChangesAsync();

        var controller = new PortalApiController(
            dbContext,
            new FakeCurrentUserService { UserId = 88, UserName = "client@one.local", Role = ApplicationRoles.Client },
            new FakeAuditService(),
            new FakeEventOutboxService())
        {
            ControllerContext = BuildControllerContext(
                ApplicationRoles.Client,
                clientAccountId: 1,
                traceIdentifier: "portal-trace-2")
        };

        ActionResult<IReadOnlyList<PortalDisputeSummaryDto>> actionResult = await controller.GetDisputes(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var disputes = Assert.IsAssignableFrom<IReadOnlyList<PortalDisputeSummaryDto>>(ok.Value);

        Assert.Single(disputes);
        Assert.Equal("DSP-ONE", disputes[0].DisputeReference);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static ControllerContext BuildControllerContext(string role, int? clientAccountId, string traceIdentifier)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Test User"),
            new(ClaimTypes.Role, role)
        };

        if (clientAccountId.HasValue)
        {
            claims.Add(new Claim("ClientAccountId", clientAccountId.Value.ToString()));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var httpContext = new DefaultHttpContext
        {
            User = principal,
            TraceIdentifier = traceIdentifier
        };

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }
}
