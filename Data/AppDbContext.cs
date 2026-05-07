using Microsoft.EntityFrameworkCore;
using MonetaCore.Models;

namespace MonetaCore.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ClientAccount> Clients => Set<ClientAccount>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLineItem> InvoiceItems => Set<InvoiceLineItem>();
    public DbSet<PaymentTransaction> Payments => Set<PaymentTransaction>();
    public DbSet<CreditDebitAdjustment> Adjustments => Set<CreditDebitAdjustment>();
    public DbSet<AccountIntegrationEvent> IntegrationEvents => Set<AccountIntegrationEvent>();
    public DbSet<AuditTrailEntry> AuditTrail => Set<AuditTrailEntry>();
    public DbSet<PortalDispute> PortalDisputes => Set<PortalDispute>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Invoice>()
            .HasIndex(x => x.InvoiceNumber)
            .IsUnique();

        modelBuilder.Entity<Invoice>()
            .Property(x => x.Subtotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Invoice>()
            .Property(x => x.TaxRate)
            .HasPrecision(5, 4);

        modelBuilder.Entity<Invoice>()
            .Property(x => x.TaxAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Invoice>()
            .Property(x => x.AdjustmentTotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Invoice>()
            .Property(x => x.TotalAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Invoice>()
            .Property(x => x.AmountPaid)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Invoice>()
            .Property(x => x.BalanceDue)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceLineItem>()
            .Property(x => x.Quantity)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceLineItem>()
            .Property(x => x.UnitPrice)
            .HasPrecision(18, 2);

        modelBuilder.Entity<InvoiceLineItem>()
            .Property(x => x.LineTotal)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PaymentTransaction>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<CreditDebitAdjustment>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<OutboxMessage>()
            .HasIndex(x => x.EventId)
            .IsUnique();

        modelBuilder.Entity<OutboxMessage>()
            .HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.CreatedAtUtc });

        modelBuilder.Entity<PortalDispute>()
            .HasIndex(x => x.DisputeReference)
            .IsUnique();

        modelBuilder.Entity<PortalDispute>()
            .HasIndex(x => new { x.Status, x.SubmittedAtUtc });

        modelBuilder.Entity<AccountIntegrationEvent>()
            .HasIndex(x => x.CorrelationId);

        modelBuilder.Entity<AppUser>()
            .HasOne(x => x.ClientAccount)
            .WithMany(x => x.Users)
            .HasForeignKey(x => x.ClientAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Invoice>()
            .HasOne(x => x.CreatedByUser)
            .WithMany(x => x.AuthoredInvoices)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvoiceLineItem>()
            .HasOne(x => x.Invoice)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PaymentTransaction>()
            .HasOne(x => x.Invoice)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PaymentTransaction>()
            .HasOne(x => x.ProcessedByUser)
            .WithMany(x => x.ProcessedPayments)
            .HasForeignKey(x => x.ProcessedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CreditDebitAdjustment>()
            .HasOne(x => x.Invoice)
            .WithMany(x => x.Adjustments)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CreditDebitAdjustment>()
            .HasOne(x => x.CreatedByUser)
            .WithMany(x => x.CreatedAdjustments)
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CreditDebitAdjustment>()
            .HasOne(x => x.ApprovedByUser)
            .WithMany(x => x.ApprovedAdjustments)
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PortalDispute>()
            .HasOne(x => x.Invoice)
            .WithMany(x => x.Disputes)
            .HasForeignKey(x => x.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PortalDispute>()
            .HasOne(x => x.SubmittedByUser)
            .WithMany(x => x.SubmittedPortalDisputes)
            .HasForeignKey(x => x.SubmittedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AccountIntegrationEvent>()
            .HasOne(x => x.TriggeredByUser)
            .WithMany()
            .HasForeignKey(x => x.TriggeredByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AuditTrailEntry>()
            .HasOne(x => x.User)
            .WithMany(x => x.AuditTrailEntries)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
