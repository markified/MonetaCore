using MonetaCore.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MonetaCore.Services;

public sealed class ProfitLossPdfDocument : IDocument
{
    private readonly ProfitLossReportViewModel _model;

    public ProfitLossPdfDocument(ProfitLossReportViewModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = "MonetaCore Profit & Loss Report",
        Author = "MonetaCore",
        Subject = "Revenue vs expenses report"
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Header().Column(column =>
            {
                column.Spacing(4);
                column.Item().Text("MonetaCore").FontSize(20).Bold().FontColor(Colors.Amber.Darken2);
                column.Item().Text("Profit & Loss Report").FontSize(15).SemiBold();
                column.Item().Text($"Period: {_model.PeriodLabel}").FontSize(10);
            });

            page.Content().Column(column =>
            {
                column.Spacing(16);

                column.Item().Row(row =>
                {
                    row.Spacing(12);
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Revenue", _model.RevenueTotal, Colors.Green.Darken2));
                    row.RelativeItem().Element(card => ComposeMetricCard(card, "Expenses", _model.ExpenseTotal, Colors.Red.Darken2));
                    row.RelativeItem().Element(card => ComposeMetricCard(card, _model.NetProfit >= 0 ? "Net Profit" : "Net Loss", Math.Abs(_model.NetProfit), _model.NetProfit >= 0 ? Colors.Amber.Darken2 : Colors.Red.Darken2));
                    row.RelativeItem().Element(card => ComposeTextCard(card, "Margin", $"{_model.ProfitMarginPercent:N2}%"));
                });

                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(9));
                    text.Span("Expense basis: ").SemiBold();
                    text.Span("This project-level P&L uses recorded credit-note adjustments as expense entries because MonetaCore does not yet include a dedicated vendor/AP expense ledger.");
                });

                column.Item().Text("Revenue Entries").FontSize(12).Bold().FontColor(Colors.Amber.Darken2);
                if (_model.RevenueEntries.Count == 0)
                {
                    column.Item().Text("No completed payments were recorded within the selected period.");
                }
                else
                {
                    column.Item().Element(ComposeRevenueTable);
                }

                column.Item().Text("Expense Entries").FontSize(12).Bold().FontColor(Colors.Amber.Darken2);
                if (_model.ExpenseEntries.Count == 0)
                {
                    column.Item().Text("No credit-note adjustments were recorded within the selected period.");
                }
                else
                {
                    column.Item().Element(ComposeExpenseTable);
                }
            });

            page.Footer().AlignRight().Text(text =>
            {
                text.Span($"Generated {_model.GeneratedAtLocal:MMM dd, yyyy hh:mm tt} • Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    private static void ComposeMetricCard(IContainer container, string label, decimal amount, string accentColor)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(10)
            .Column(column =>
            {
                column.Spacing(4);
                column.Item().Text(label).FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                column.Item().Text(amount.ToString("N2")).FontSize(16).Bold().FontColor(accentColor);
            });
    }

    private static void ComposeTextCard(IContainer container, string label, string value)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(10)
            .Column(column =>
            {
                column.Spacing(4);
                column.Item().Text(label).FontSize(9).SemiBold().FontColor(Colors.Grey.Darken2);
                column.Item().Text(value).FontSize(16).Bold().FontColor(Colors.Amber.Darken2);
            });
    }

    private void ComposeRevenueTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(85);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(1.2f);
                columns.ConstantColumn(90);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCellStyle).Text("Date").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).Text("Invoice").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).Text("Client").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).Text("Method").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).Text("Reference").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).AlignRight().Text("Amount").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
            });

            foreach (ProfitLossRevenueEntryViewModel entry in _model.RevenueEntries)
            {
                BodyCell(table, entry.PaidAtUtc.ToLocalTime().ToString("MMM dd, yyyy"));
                BodyCell(table, entry.InvoiceNumber);
                BodyCell(table, entry.ClientName);
                BodyCell(table, entry.Method);
                BodyCell(table, string.IsNullOrWhiteSpace(entry.ReferenceNumber) ? "-" : entry.ReferenceNumber);
                BodyCell(table, entry.Amount.ToString("N2"), alignRight: true);
            }
        });
    }

    private void ComposeExpenseTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(85);
                columns.RelativeColumn(1.1f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.8f);
                columns.ConstantColumn(90);
            });

            table.Header(header =>
            {
                header.Cell().Element(TableHeaderCellStyle).Text("Date").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).Text("Invoice").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).Text("Client").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).Text("Reason").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
                header.Cell().Element(TableHeaderCellStyle).AlignRight().Text("Amount").SemiBold().FontSize(9).FontColor(Colors.Amber.Darken2);
            });

            foreach (ProfitLossExpenseEntryViewModel entry in _model.ExpenseEntries)
            {
                BodyCell(table, entry.AppliedAtUtc.ToLocalTime().ToString("MMM dd, yyyy"));
                BodyCell(table, entry.InvoiceNumber);
                BodyCell(table, entry.ClientName);
                BodyCell(table, string.IsNullOrWhiteSpace(entry.Reason) ? "No reason provided" : entry.Reason);
                BodyCell(table, entry.Amount.ToString("N2"), alignRight: true);
            }
        });
    }

    private static IContainer TableHeaderCellStyle(IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .PaddingVertical(6)
            .PaddingHorizontal(4);
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Element(TableBodyCellStyle);

        if (alignRight)
        {
            cell.AlignRight().Text(text);
            return;
        }

        cell.AlignLeft().Text(text);
    }

    private static IContainer TableBodyCellStyle(IContainer container)
    {
        return container
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(6)
            .PaddingHorizontal(4);
    }
}