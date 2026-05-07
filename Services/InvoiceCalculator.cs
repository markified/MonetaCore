using MonetaCore.Models;

namespace MonetaCore.Services;

public static class InvoiceCalculator
{
    public static void RecalculateTotals(Invoice invoice)
    {
        decimal subtotal = invoice.Items.Sum(x => x.LineTotal);
        decimal taxAmount = subtotal * invoice.TaxRate;
        decimal total = subtotal + taxAmount + invoice.AdjustmentTotal;
        if (total < 0)
        {
            total = 0;
        }

        invoice.Subtotal = Round(subtotal);
        invoice.TaxAmount = Round(taxAmount);
        invoice.TotalAmount = Round(total);
        invoice.BalanceDue = Round(invoice.TotalAmount - invoice.AmountPaid);
        invoice.UpdatedAtUtc = DateTime.UtcNow;

        if (invoice.BalanceDue <= 0)
        {
            invoice.BalanceDue = 0;
            invoice.Status = DomainValues.InvoiceStatus.Paid;
        }
        else if (invoice.AmountPaid > 0)
        {
            invoice.Status = DomainValues.InvoiceStatus.PartiallyPaid;
        }
        else
        {
            invoice.Status = invoice.DueDateUtc < DateTime.UtcNow
                ? DomainValues.InvoiceStatus.Overdue
                : DomainValues.InvoiceStatus.Issued;
        }
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
