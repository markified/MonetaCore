using Microsoft.EntityFrameworkCore;
using MonetaCore.Data;

namespace MonetaCore.Services;

public class InvoiceNumberService : IInvoiceNumberService
{
    private readonly AppDbContext _dbContext;

    public InvoiceNumberService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        int year = DateTime.UtcNow.Year;
        string prefix = $"INV-{year}-";

        int existingCount = await _dbContext.Invoices
            .CountAsync(x => x.InvoiceNumber.StartsWith(prefix), cancellationToken);

        return $"{prefix}{existingCount + 1:00000}";
    }
}
