using Microsoft.EntityFrameworkCore;
using MonetaCore.Models;
using MonetaCore.Services;

namespace MonetaCore.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext dbContext, IPasswordService passwordService)
    {
        await dbContext.Database.EnsureCreatedAsync();

        if (await dbContext.Users.AnyAsync())
        {
            await EnsureSuperAdminAsync(dbContext, passwordService);
            return;
        }

        var alpha = new ClientAccount
        {
            CompanyName = "Alpha Service Solutions",
            ContactPerson = "Rina Alvarez",
            Email = "billing@alphaservices.ph",
            Phone = "+63-917-111-0101",
            Address = "Makati City, Metro Manila"
        };

        var nova = new ClientAccount
        {
            CompanyName = "Nova Creative Works",
            ContactPerson = "Jude Serrano",
            Email = "finance@novacreative.ph",
            Phone = "+63-917-222-0202",
            Address = "Cebu City, Cebu"
        };

        dbContext.Clients.AddRange(alpha, nova);
        await dbContext.SaveChangesAsync();

        var superAdmin = new AppUser
        {
            FullName = "Super Administrator",
            Email = "superadmin@monetacore.local",
            PasswordHash = passwordService.HashPassword("SuperAdmin@123"),
            Role = ApplicationRoles.SuperAdmin,
            IsActive = true
        };

        var admin = new AppUser
        {
            FullName = "Main Administrator",
            Email = "admin@monetacore.local",
            PasswordHash = passwordService.HashPassword("Admin@123"),
            Role = ApplicationRoles.MainAdmin,
            IsActive = true
        };

        var finance = new AppUser
        {
            FullName = "Finance Manager",
            Email = "finance@monetacore.local",
            PasswordHash = passwordService.HashPassword("Finance@123"),
            Role = ApplicationRoles.FinanceManager,
            IsActive = true
        };

        var billing = new AppUser
        {
            FullName = "Billing Staff",
            Email = "billing@monetacore.local",
            PasswordHash = passwordService.HashPassword("Billing@123"),
            Role = ApplicationRoles.BillingStaff,
            IsActive = true
        };

        var accountant = new AppUser
        {
            FullName = "Accountant",
            Email = "accountant@monetacore.local",
            PasswordHash = passwordService.HashPassword("Accountant@123"),
            Role = ApplicationRoles.Accountant,
            IsActive = true
        };

        var auditor = new AppUser
        {
            FullName = "Audit Viewer",
            Email = "auditor@monetacore.local",
            PasswordHash = passwordService.HashPassword("Auditor@123"),
            Role = ApplicationRoles.Auditor,
            IsActive = true
        };

        var externalClient = new AppUser
        {
            FullName = "Client Portal User",
            Email = "client@monetacore.local",
            PasswordHash = passwordService.HashPassword("Client@123"),
            Role = ApplicationRoles.Client,
            ClientAccountId = alpha.Id,
            IsActive = true
        };

        dbContext.Users.AddRange(superAdmin, admin, finance, billing, accountant, auditor, externalClient);
        await dbContext.SaveChangesAsync();

        var invoice1 = new Invoice
        {
            InvoiceNumber = "INV-2026-00001",
            ClientAccountId = alpha.Id,
            CreatedByUserId = billing.Id,
            IssueDateUtc = DateTime.UtcNow.AddDays(-12),
            DueDateUtc = DateTime.UtcNow.AddDays(3),
            TaxRate = 0.12m,
            Notes = "Retainer for support and maintenance services"
        };

        invoice1.Items.Add(new InvoiceLineItem { Description = "Monthly support retainer", Quantity = 1, UnitPrice = 45000m, LineTotal = 45000m });
        invoice1.Items.Add(new InvoiceLineItem { Description = "On-call technical support", Quantity = 10, UnitPrice = 1500m, LineTotal = 15000m });
        InvoiceCalculator.RecalculateTotals(invoice1);

        var invoice2 = new Invoice
        {
            InvoiceNumber = "INV-2026-00002",
            ClientAccountId = nova.Id,
            CreatedByUserId = billing.Id,
            IssueDateUtc = DateTime.UtcNow.AddDays(-20),
            DueDateUtc = DateTime.UtcNow.AddDays(-2),
            TaxRate = 0.12m,
            Notes = "Creative campaign project billing"
        };

        invoice2.Items.Add(new InvoiceLineItem { Description = "Concept design", Quantity = 1, UnitPrice = 32000m, LineTotal = 32000m });
        invoice2.Items.Add(new InvoiceLineItem { Description = "Media rollout", Quantity = 1, UnitPrice = 25000m, LineTotal = 25000m });
        InvoiceCalculator.RecalculateTotals(invoice2);

        var payment = new PaymentTransaction
        {
            Invoice = invoice1,
            ProcessedByUserId = billing.Id,
            Amount = 25000m,
            Method = DomainValues.PaymentMethod.Cash,
            ReferenceNumber = "CASH-982174",
            Status = DomainValues.PaymentStatus.Completed,
            Notes = "Initial partial payment",
            PaidAtUtc = DateTime.UtcNow.AddDays(-5)
        };

        invoice1.AmountPaid += payment.Amount;
        InvoiceCalculator.RecalculateTotals(invoice1);

        dbContext.Invoices.AddRange(invoice1, invoice2);
        dbContext.Payments.Add(payment);

        dbContext.AuditTrail.Add(new AuditTrailEntry
        {
            UserId = admin.Id,
            UserName = admin.FullName,
            Action = "SEED",
            EntityName = "System",
            EntityId = "Bootstrap",
            Metadata = "Initial seed data created",
            TimestampUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task EnsureSuperAdminAsync(AppDbContext dbContext, IPasswordService passwordService)
    {
        const string superAdminEmail = "superadmin@monetacore.local";
        string normalizedEmail = superAdminEmail.ToLowerInvariant();

        var superAdmin = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Email.ToLower() == normalizedEmail);

        if (superAdmin is null)
        {
            dbContext.Users.Add(new AppUser
            {
                FullName = "Super Administrator",
                Email = superAdminEmail,
                PasswordHash = passwordService.HashPassword("SuperAdmin@123"),
                Role = ApplicationRoles.SuperAdmin,
                IsActive = true
            });

            await dbContext.SaveChangesAsync();
            return;
        }

        bool hasChanges = false;

        if (!superAdmin.IsActive)
        {
            superAdmin.IsActive = true;
            hasChanges = true;
        }

        if (!string.Equals(superAdmin.Role, ApplicationRoles.SuperAdmin, StringComparison.Ordinal))
        {
            superAdmin.Role = ApplicationRoles.SuperAdmin;
            hasChanges = true;
        }

        if (string.IsNullOrWhiteSpace(superAdmin.FullName))
        {
            superAdmin.FullName = "Super Administrator";
            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync();
        }
    }
}
