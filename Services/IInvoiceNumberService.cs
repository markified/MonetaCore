namespace MonetaCore.Services;

public interface IInvoiceNumberService
{
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
