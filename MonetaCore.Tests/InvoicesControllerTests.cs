using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonetaCore.Controllers;
using MonetaCore.Data;
using MonetaCore.Models;
using MonetaCore.ViewModels;

namespace MonetaCore.Tests;

public class InvoicesControllerTests
{
    [Fact]
    public async Task Index_AppliesFilteringAndPagination()
    {
        await using var dbContext = CreateDbContext();

        var clientA = new ClientAccount
        {
            Id = 1,
            CompanyName = "Alpha Corp",
            ContactPerson = "Alpha Admin",
            Email = "alpha@local",
            Phone = "111",
            Address = "Alpha"
        };

        var clientB = new ClientAccount
        {
            Id = 2,
            CompanyName = "Beta Corp",
            ContactPerson = "Beta Admin",
            Email = "beta@local",
            Phone = "222",
            Address = "Beta"
        };

        dbContext.Clients.AddRange(clientA, clientB);

        for (int i = 1; i <= 15; i++)
        {
            dbContext.Invoices.Add(new Invoice
            {
                InvoiceNumber = $"INV-ALPHA-{i:000}",
                ClientAccountId = 1,
                CreatedByUserId = 1,
                Status = DomainValues.InvoiceStatus.Issued,
                TotalAmount = 100m,
                BalanceDue = 100m,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-i)
            });
        }

        dbContext.Invoices.Add(new Invoice
        {
            InvoiceNumber = "INV-BETA-001",
            ClientAccountId = 2,
            CreatedByUserId = 1,
            Status = DomainValues.InvoiceStatus.Paid,
            TotalAmount = 50m,
            AmountPaid = 50m,
            BalanceDue = 0m,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, ApplicationRoles.SuperAdmin);

        IActionResult result = await controller.Index(
            search: "ALPHA",
            status: DomainValues.InvoiceStatus.Issued,
            page: 2,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InvoiceIndexViewModel>(view.Model);

        Assert.Equal(15, model.TotalCount);
        Assert.Equal(2, model.TotalPages);
        Assert.Equal(2, model.Page);
        Assert.Equal(10, model.PageSize);
        Assert.Equal(5, model.Invoices.Count);
        Assert.All(model.Invoices, x => Assert.Contains("ALPHA", x.InvoiceNumber));
        Assert.All(model.Invoices, x => Assert.Equal(DomainValues.InvoiceStatus.Issued, x.Status));
    }

    [Fact]
    public async Task Index_ClientRole_OnlySeesOwnInvoices()
    {
        await using var dbContext = CreateDbContext();

        dbContext.Clients.AddRange(
            new ClientAccount
            {
                Id = 1,
                CompanyName = "Client One",
                ContactPerson = "A",
                Email = "one@local",
                Phone = "111",
                Address = "One"
            },
            new ClientAccount
            {
                Id = 2,
                CompanyName = "Client Two",
                ContactPerson = "B",
                Email = "two@local",
                Phone = "222",
                Address = "Two"
            });

        dbContext.Invoices.AddRange(
            new Invoice
            {
                InvoiceNumber = "INV-ONE-001",
                ClientAccountId = 1,
                CreatedByUserId = 1,
                Status = DomainValues.InvoiceStatus.Issued,
                TotalAmount = 100m,
                BalanceDue = 100m
            },
            new Invoice
            {
                InvoiceNumber = "INV-TWO-001",
                ClientAccountId = 2,
                CreatedByUserId = 1,
                Status = DomainValues.InvoiceStatus.Issued,
                TotalAmount = 200m,
                BalanceDue = 200m
            });

        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, ApplicationRoles.Client, clientAccountId: 1);

        IActionResult result = await controller.Index(cancellationToken: CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<InvoiceIndexViewModel>(view.Model);

        Assert.Single(model.Invoices);
        Assert.Equal("INV-ONE-001", model.Invoices[0].InvoiceNumber);
        Assert.Equal(1, model.TotalCount);
    }

    private static InvoicesController CreateController(AppDbContext dbContext, string role, int? clientAccountId = null)
    {
        var controller = new InvoicesController(
            dbContext,
            new FakeCurrentUserService { UserId = 88, UserName = "tester@local", Role = role },
            new FakeInvoiceNumberService(),
            new FakeAuditService())
        {
            ControllerContext = BuildControllerContext(role, clientAccountId)
        };

        return controller;
    }

    private static ControllerContext BuildControllerContext(string role, int? clientAccountId)
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
            User = principal
        };

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }
}
