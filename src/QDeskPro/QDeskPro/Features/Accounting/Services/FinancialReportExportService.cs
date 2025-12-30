using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using QDeskPro.Features.Accounting.Models;
using QDeskPro.Domain.Enums;

namespace QDeskPro.Features.Accounting.Services;

/// <summary>
/// Service for exporting financial reports to PDF and Excel formats.
/// </summary>
public interface IFinancialReportExportService
{
    // PDF Exports
    byte[] ExportTrialBalanceToPdf(TrialBalanceReport report);
    byte[] ExportProfitLossToPdf(ProfitLossReport report);
    byte[] ExportBalanceSheetToPdf(BalanceSheetReport report);
    byte[] ExportCashFlowToPdf(CashFlowReport report);
    byte[] ExportARAgingToPdf(ARAgingReport report);
    byte[] ExportAPSummaryToPdf(APSummaryReport report);

    // Excel Exports
    byte[] ExportTrialBalanceToExcel(TrialBalanceReport report);
    byte[] ExportProfitLossToExcel(ProfitLossReport report);
    byte[] ExportBalanceSheetToExcel(BalanceSheetReport report);
    byte[] ExportCashFlowToExcel(CashFlowReport report);
    byte[] ExportARAgingToExcel(ARAgingReport report);
    byte[] ExportAPSummaryToExcel(APSummaryReport report);

    // Combined Financial Package
    byte[] ExportFinancialPackageToExcel(
        TrialBalanceReport trialBalance,
        ProfitLossReport profitLoss,
        BalanceSheetReport balanceSheet,
        CashFlowReport cashFlow,
        ARAgingReport arAging,
        APSummaryReport apSummary);
}

/// <summary>
/// Implementation of financial report export service.
/// </summary>
public class FinancialReportExportService : IFinancialReportExportService
{
    // Brand colors
    private static readonly Color PrimaryColor = Color.FromHex("#1976D2");
    private static readonly Color PrimaryDark = Color.FromHex("#0D47A1");
    private static readonly Color SuccessColor = Color.FromHex("#4CAF50");
    private static readonly Color ErrorColor = Color.FromHex("#F44336");
    private static readonly Color WarningColor = Color.FromHex("#FF9800");
    private static readonly Color TextPrimary = Color.FromHex("#212121");
    private static readonly Color TextSecondary = Color.FromHex("#757575");
    private static readonly Color BackgroundLight = Color.FromHex("#FAFAFA");
    private static readonly Color BorderColor = Color.FromHex("#E0E0E0");

    static FinancialReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    #region Trial Balance

    public byte[] ExportTrialBalanceToPdf(TrialBalanceReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextPrimary));

                page.Header().Element(c => ComposeFinancialHeader(c, "TRIAL BALANCE", report.QuarryName, $"As of {report.AsOfDate:MMMM dd, yyyy}"));

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(10);

                    // Summary info
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Total Debits: KES {report.TotalDebits:N2}").Bold().FontColor(PrimaryColor);
                            c.Item().Text($"Total Credits: KES {report.TotalCredits:N2}").Bold().FontColor(SuccessColor);
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text(report.IsBalanced ? "BALANCED" : "OUT OF BALANCE")
                                .Bold().FontColor(report.IsBalanced ? SuccessColor : ErrorColor);
                            if (!report.IsBalanced)
                                c.Item().Text($"Difference: KES {report.Difference:N2}").FontColor(ErrorColor);
                        });
                    });

                    // Table
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(60);   // Account Code
                            columns.RelativeColumn(2);    // Account Name
                            columns.ConstantColumn(80);   // Category
                            columns.ConstantColumn(80);   // Debit
                            columns.ConstantColumn(80);   // Credit
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Background(PrimaryColor).Padding(5).Text("CODE").FontSize(9).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).Text("ACCOUNT NAME").FontSize(9).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).Text("CATEGORY").FontSize(9).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).AlignRight().Text("DEBIT").FontSize(9).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).AlignRight().Text("CREDIT").FontSize(9).Bold().FontColor(Colors.White);
                        });

                        // Group by category
                        foreach (var categoryGroup in report.Lines.GroupBy(l => l.Category).OrderBy(g => (int)g.Key))
                        {
                            // Category header
                            table.Cell().ColumnSpan(5).Background(BackgroundLight).Padding(5)
                                .Text(categoryGroup.Key.ToString().ToUpper()).FontSize(9).Bold().FontColor(PrimaryDark);

                            foreach (var line in categoryGroup.OrderBy(l => l.AccountCode))
                            {
                                table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).Text(line.AccountCode).FontSize(9);
                                table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).Text(line.AccountName).FontSize(9);
                                table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).Text(line.Category.ToString()).FontSize(8).FontColor(TextSecondary);
                                table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                                    .Text(line.DebitBalance > 0 ? $"{line.DebitBalance:N2}" : "").FontSize(9);
                                table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                                    .Text(line.CreditBalance > 0 ? $"{line.CreditBalance:N2}" : "").FontSize(9);
                            }
                        }

                        // Total row
                        table.Cell().ColumnSpan(3).Background(PrimaryDark).Padding(5).AlignRight()
                            .Text("TOTALS:").FontSize(10).Bold().FontColor(Colors.White);
                        table.Cell().Background(PrimaryDark).Padding(5).AlignRight()
                            .Text($"{report.TotalDebits:N2}").FontSize(10).Bold().FontColor(Colors.White);
                        table.Cell().Background(PrimaryDark).Padding(5).AlignRight()
                            .Text($"{report.TotalCredits:N2}").FontSize(10).Bold().FontColor(Colors.White);
                    });
                });

                page.Footer().Element(ComposeFinancialFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportTrialBalanceToExcel(TrialBalanceReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Trial Balance");

        // Title
        sheet.Cell(1, 1).Value = "TRIAL BALANCE";
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = report.QuarryName;
        sheet.Cell(3, 1).Value = $"As of {report.AsOfDate:MMMM dd, yyyy}";

        // Headers
        int row = 5;
        sheet.Cell(row, 1).Value = "Account Code";
        sheet.Cell(row, 2).Value = "Account Name";
        sheet.Cell(row, 3).Value = "Category";
        sheet.Cell(row, 4).Value = "Debit";
        sheet.Cell(row, 5).Value = "Credit";
        sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = HexToXLColor("#1976D2");
        sheet.Range(row, 1, row, 5).Style.Font.FontColor = XLColor.White;

        row++;
        foreach (var line in report.Lines.OrderBy(l => l.AccountCode))
        {
            sheet.Cell(row, 1).Value = line.AccountCode;
            sheet.Cell(row, 2).Value = line.AccountName;
            sheet.Cell(row, 3).Value = line.Category.ToString();
            sheet.Cell(row, 4).Value = line.DebitBalance > 0 ? line.DebitBalance : 0;
            sheet.Cell(row, 5).Value = line.CreditBalance > 0 ? line.CreditBalance : 0;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        // Totals
        sheet.Cell(row, 3).Value = "TOTALS:";
        sheet.Cell(row, 3).Style.Font.Bold = true;
        sheet.Cell(row, 4).Value = report.TotalDebits;
        sheet.Cell(row, 5).Value = report.TotalCredits;
        sheet.Range(row, 3, row, 5).Style.Font.Bold = true;
        sheet.Range(row, 3, row, 5).Style.Fill.BackgroundColor = HexToXLColor("#E3F2FD");
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region Profit & Loss

    public byte[] ExportProfitLossToPdf(ProfitLossReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextPrimary));

                page.Header().Element(c => ComposeFinancialHeader(c, "PROFIT & LOSS STATEMENT",
                    report.QuarryName, $"For Period: {report.PeriodStart:MMM dd} - {report.PeriodEnd:MMM dd, yyyy}"));

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(15);

                    // Revenue Section
                    column.Item().Element(c => ComposePnLSection(c, "REVENUE", report.RevenueItems,
                        report.TotalRevenue, SuccessColor, true));

                    // Cost of Sales Section
                    column.Item().Element(c => ComposePnLSection(c, "COST OF SALES", report.CostOfSalesItems,
                        report.TotalCostOfSales, WarningColor, false));

                    // Gross Profit
                    column.Item().Background(Color.FromHex("#E8F5E9")).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("GROSS PROFIT").Bold().FontSize(11);
                        row.AutoItem().Text($"KES {report.GrossProfit:N0}").Bold().FontSize(11).FontColor(SuccessColor);
                        row.AutoItem().PaddingLeft(20).Text($"({report.GrossProfitMargin:N1}%)").FontSize(9).FontColor(TextSecondary);
                    });

                    // Operating Expenses Section
                    column.Item().Element(c => ComposePnLSection(c, "OPERATING EXPENSES", report.OperatingExpenses,
                        report.TotalOperatingExpenses, ErrorColor, false));

                    // Operating Profit
                    column.Item().Background(Color.FromHex("#E3F2FD")).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("OPERATING PROFIT").Bold().FontSize(11);
                        row.AutoItem().Text($"KES {report.OperatingProfit:N0}").Bold().FontSize(11).FontColor(PrimaryColor);
                        row.AutoItem().PaddingLeft(20).Text($"({report.OperatingProfitMargin:N1}%)").FontSize(9).FontColor(TextSecondary);
                    });

                    // Net Profit (highlighted)
                    column.Item().Background(report.NetProfit >= 0 ? SuccessColor : ErrorColor).Padding(15).Row(row =>
                    {
                        row.RelativeItem().Text("NET PROFIT").Bold().FontSize(14).FontColor(Colors.White);
                        row.AutoItem().Text($"KES {report.NetProfit:N0}").Bold().FontSize(14).FontColor(Colors.White);
                        row.AutoItem().PaddingLeft(20).Text($"({report.NetProfitMargin:N1}%)").FontSize(10).FontColor(Colors.White);
                    });
                });

                page.Footer().Element(ComposeFinancialFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportProfitLossToExcel(ProfitLossReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Profit & Loss");

        // Title
        sheet.Cell(1, 1).Value = "PROFIT & LOSS STATEMENT";
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = report.QuarryName;
        sheet.Cell(3, 1).Value = $"For Period: {report.PeriodStart:MMM dd} - {report.PeriodEnd:MMM dd, yyyy}";

        int row = 5;

        // Revenue
        sheet.Cell(row, 1).Value = "REVENUE";
        sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#4CAF50");
        sheet.Range(row, 1, row, 3).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
        row++;

        foreach (var item in report.RevenueItems)
        {
            sheet.Cell(row, 1).Value = item.AccountCode;
            sheet.Cell(row, 2).Value = item.Description;
            sheet.Cell(row, 3).Value = item.Amount;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            row++;
        }
        sheet.Cell(row, 2).Value = "Total Revenue";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalRevenue;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 3).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#E8F5E9");
        row += 2;

        // Cost of Sales
        sheet.Cell(row, 1).Value = "COST OF SALES";
        sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#FF9800");
        sheet.Range(row, 1, row, 3).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
        row++;

        foreach (var item in report.CostOfSalesItems)
        {
            sheet.Cell(row, 1).Value = item.AccountCode;
            sheet.Cell(row, 2).Value = item.Description;
            sheet.Cell(row, 3).Value = item.Amount;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            row++;
        }
        sheet.Cell(row, 2).Value = "Total Cost of Sales";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalCostOfSales;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 3).Style.Font.Bold = true;
        row += 2;

        // Gross Profit
        sheet.Cell(row, 2).Value = "GROSS PROFIT";
        sheet.Cell(row, 3).Value = report.GrossProfit;
        sheet.Cell(row, 4).Value = $"{report.GrossProfitMargin:N1}%";
        sheet.Range(row, 2, row, 4).Style.Font.Bold = true;
        sheet.Range(row, 2, row, 4).Style.Fill.BackgroundColor = HexToXLColor("#E8F5E9");
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        row += 2;

        // Operating Expenses
        sheet.Cell(row, 1).Value = "OPERATING EXPENSES";
        sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#F44336");
        sheet.Range(row, 1, row, 3).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
        row++;

        foreach (var item in report.OperatingExpenses)
        {
            sheet.Cell(row, 1).Value = item.AccountCode;
            sheet.Cell(row, 2).Value = item.Description;
            sheet.Cell(row, 3).Value = item.Amount;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            row++;
        }
        sheet.Cell(row, 2).Value = "Total Operating Expenses";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalOperatingExpenses;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 3).Style.Font.Bold = true;
        row += 2;

        // Net Profit
        sheet.Cell(row, 2).Value = "NET PROFIT";
        sheet.Cell(row, 3).Value = report.NetProfit;
        sheet.Cell(row, 4).Value = $"{report.NetProfitMargin:N1}%";
        sheet.Range(row, 2, row, 4).Style.Font.Bold = true;
        sheet.Range(row, 2, row, 4).Style.Font.FontSize = 14;
        sheet.Range(row, 2, row, 4).Style.Fill.BackgroundColor = report.NetProfit >= 0 ? HexToXLColor("#4CAF50") : HexToXLColor("#F44336");
        sheet.Range(row, 2, row, 4).Style.Font.FontColor = XLColor.White;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region Balance Sheet

    public byte[] ExportBalanceSheetToPdf(BalanceSheetReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextPrimary));

                page.Header().Element(c => ComposeFinancialHeader(c, "BALANCE SHEET",
                    report.QuarryName, $"As of {report.AsOfDate:MMMM dd, yyyy}"));

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(15);

                    // Assets Section
                    column.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Background(SuccessColor).Padding(8)
                            .Text("ASSETS").FontSize(11).Bold().FontColor(Colors.White);

                        col.Item().Padding(10).Column(assetCol =>
                        {
                            // Current Assets
                            assetCol.Item().Text("Current Assets").FontSize(9).Bold().FontColor(TextSecondary);
                            foreach (var item in report.CurrentAssets)
                            {
                                assetCol.Item().Row(row =>
                                {
                                    row.ConstantItem(60).Text(item.AccountCode).FontSize(9).FontColor(TextSecondary);
                                    row.RelativeItem().Text(item.Description).FontSize(9);
                                    row.ConstantItem(80).AlignRight().Text($"{item.Amount:N0}").FontSize(9);
                                });
                            }
                            assetCol.Item().PaddingTop(5).Row(row =>
                            {
                                row.RelativeItem().Text("Total Current Assets").FontSize(9).Bold();
                                row.ConstantItem(80).AlignRight().Text($"{report.TotalCurrentAssets:N0}").FontSize(9).Bold();
                            });

                            assetCol.Item().PaddingTop(10).Text("Non-Current Assets").FontSize(9).Bold().FontColor(TextSecondary);
                            foreach (var item in report.NonCurrentAssets)
                            {
                                assetCol.Item().Row(row =>
                                {
                                    row.ConstantItem(60).Text(item.AccountCode).FontSize(9).FontColor(TextSecondary);
                                    row.RelativeItem().Text(item.Description).FontSize(9);
                                    row.ConstantItem(80).AlignRight().Text($"{item.Amount:N0}").FontSize(9);
                                });
                            }
                            assetCol.Item().PaddingTop(5).Row(row =>
                            {
                                row.RelativeItem().Text("Total Non-Current Assets").FontSize(9).Bold();
                                row.ConstantItem(80).AlignRight().Text($"{report.TotalNonCurrentAssets:N0}").FontSize(9).Bold();
                            });
                        });

                        col.Item().Background(Color.FromHex("#E8F5E9")).Padding(8).Row(row =>
                        {
                            row.RelativeItem().Text("TOTAL ASSETS").FontSize(10).Bold();
                            row.ConstantItem(100).AlignRight().Text($"KES {report.TotalAssets:N0}").FontSize(10).Bold().FontColor(SuccessColor);
                        });
                    });

                    // Liabilities Section
                    column.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Background(ErrorColor).Padding(8)
                            .Text("LIABILITIES").FontSize(11).Bold().FontColor(Colors.White);

                        col.Item().Padding(10).Column(liabCol =>
                        {
                            liabCol.Item().Text("Current Liabilities").FontSize(9).Bold().FontColor(TextSecondary);
                            foreach (var item in report.CurrentLiabilities)
                            {
                                liabCol.Item().Row(row =>
                                {
                                    row.ConstantItem(60).Text(item.AccountCode).FontSize(9).FontColor(TextSecondary);
                                    row.RelativeItem().Text(item.Description).FontSize(9);
                                    row.ConstantItem(80).AlignRight().Text($"{item.Amount:N0}").FontSize(9);
                                });
                            }
                            liabCol.Item().PaddingTop(5).Row(row =>
                            {
                                row.RelativeItem().Text("Total Current Liabilities").FontSize(9).Bold();
                                row.ConstantItem(80).AlignRight().Text($"{report.TotalCurrentLiabilities:N0}").FontSize(9).Bold();
                            });
                        });

                        col.Item().Background(Color.FromHex("#FFEBEE")).Padding(8).Row(row =>
                        {
                            row.RelativeItem().Text("TOTAL LIABILITIES").FontSize(10).Bold();
                            row.ConstantItem(100).AlignRight().Text($"KES {report.TotalLiabilities:N0}").FontSize(10).Bold().FontColor(ErrorColor);
                        });
                    });

                    // Equity Section
                    column.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Background(PrimaryColor).Padding(8)
                            .Text("EQUITY").FontSize(11).Bold().FontColor(Colors.White);

                        col.Item().Padding(10).Column(eqCol =>
                        {
                            foreach (var item in report.EquityItems)
                            {
                                eqCol.Item().Row(row =>
                                {
                                    row.ConstantItem(60).Text(item.AccountCode).FontSize(9).FontColor(TextSecondary);
                                    row.RelativeItem().Text(item.Description).FontSize(9);
                                    row.ConstantItem(80).AlignRight().Text($"{item.Amount:N0}").FontSize(9);
                                });
                            }
                            eqCol.Item().Row(row =>
                            {
                                row.ConstantItem(60).Text("").FontSize(9);
                                row.RelativeItem().Text("Current Period Profit/(Loss)").FontSize(9).Italic();
                                row.ConstantItem(80).AlignRight().Text($"{report.CurrentPeriodProfitLoss:N0}")
                                    .FontSize(9).FontColor(report.CurrentPeriodProfitLoss >= 0 ? SuccessColor : ErrorColor);
                            });
                        });

                        col.Item().Background(Color.FromHex("#E3F2FD")).Padding(8).Row(row =>
                        {
                            row.RelativeItem().Text("TOTAL EQUITY").FontSize(10).Bold();
                            row.ConstantItem(100).AlignRight().Text($"KES {report.TotalEquity:N0}").FontSize(10).Bold().FontColor(PrimaryColor);
                        });
                    });

                    // Balance check
                    column.Item().Background(report.IsBalanced ? SuccessColor : ErrorColor).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL LIABILITIES + EQUITY").FontSize(11).Bold().FontColor(Colors.White);
                        row.ConstantItem(120).AlignRight().Text($"KES {report.TotalLiabilitiesAndEquity:N0}").FontSize(11).Bold().FontColor(Colors.White);
                    });
                });

                page.Footer().Element(ComposeFinancialFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportBalanceSheetToExcel(BalanceSheetReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Balance Sheet");

        sheet.Cell(1, 1).Value = "BALANCE SHEET";
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = report.QuarryName;
        sheet.Cell(3, 1).Value = $"As of {report.AsOfDate:MMMM dd, yyyy}";

        int row = 5;

        // Assets
        sheet.Cell(row, 1).Value = "ASSETS";
        sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#4CAF50");
        sheet.Range(row, 1, row, 3).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
        row++;

        sheet.Cell(row, 1).Value = "Current Assets";
        sheet.Cell(row, 1).Style.Font.Italic = true;
        row++;

        foreach (var item in report.CurrentAssets)
        {
            sheet.Cell(row, 1).Value = item.AccountCode;
            sheet.Cell(row, 2).Value = item.Description;
            sheet.Cell(row, 3).Value = item.Amount;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            row++;
        }

        sheet.Cell(row, 2).Value = "Total Current Assets";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalCurrentAssets;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 3).Style.Font.Bold = true;
        row += 2;

        sheet.Cell(row, 2).Value = "TOTAL ASSETS";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalAssets;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 2, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#E8F5E9");
        sheet.Range(row, 2, row, 3).Style.Font.Bold = true;
        row += 2;

        // Liabilities
        sheet.Cell(row, 1).Value = "LIABILITIES";
        sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#F44336");
        sheet.Range(row, 1, row, 3).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
        row++;

        foreach (var item in report.CurrentLiabilities)
        {
            sheet.Cell(row, 1).Value = item.AccountCode;
            sheet.Cell(row, 2).Value = item.Description;
            sheet.Cell(row, 3).Value = item.Amount;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            row++;
        }

        sheet.Cell(row, 2).Value = "TOTAL LIABILITIES";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalLiabilities;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 2, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#FFEBEE");
        sheet.Range(row, 2, row, 3).Style.Font.Bold = true;
        row += 2;

        // Equity
        sheet.Cell(row, 1).Value = "EQUITY";
        sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#1976D2");
        sheet.Range(row, 1, row, 3).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
        row++;

        foreach (var item in report.EquityItems)
        {
            sheet.Cell(row, 1).Value = item.AccountCode;
            sheet.Cell(row, 2).Value = item.Description;
            sheet.Cell(row, 3).Value = item.Amount;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            row++;
        }

        sheet.Cell(row, 2).Value = "Current Period Profit/(Loss)";
        sheet.Cell(row, 2).Style.Font.Italic = true;
        sheet.Cell(row, 3).Value = report.CurrentPeriodProfitLoss;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        row++;

        sheet.Cell(row, 2).Value = "TOTAL EQUITY";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalEquity;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 2, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#E3F2FD");
        sheet.Range(row, 2, row, 3).Style.Font.Bold = true;
        row += 2;

        // Total
        sheet.Cell(row, 2).Value = "TOTAL LIABILITIES + EQUITY";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 3).Value = report.TotalLiabilitiesAndEquity;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
        var balanced = report.IsBalanced;
        sheet.Range(row, 2, row, 3).Style.Fill.BackgroundColor = balanced ? HexToXLColor("#4CAF50") : HexToXLColor("#F44336");
        sheet.Range(row, 2, row, 3).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 2, row, 3).Style.Font.Bold = true;

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region Cash Flow

    public byte[] ExportCashFlowToPdf(CashFlowReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextPrimary));

                page.Header().Element(c => ComposeFinancialHeader(c, "CASH FLOW STATEMENT",
                    report.QuarryName, $"For Period: {report.PeriodStart:MMM dd} - {report.PeriodEnd:MMM dd, yyyy}"));

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(15);

                    // Opening Balance
                    column.Item().Background(BackgroundLight).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("OPENING CASH BALANCE").FontSize(10).Bold();
                        row.ConstantItem(120).AlignRight().Text($"KES {report.OpeningCashBalance:N0}").FontSize(10).Bold();
                    });

                    // Operating Activities
                    column.Item().Border(1).BorderColor(BorderColor).Column(col =>
                    {
                        col.Item().Background(SuccessColor).Padding(8)
                            .Text("CASH FLOWS FROM OPERATING ACTIVITIES").FontSize(10).Bold().FontColor(Colors.White);

                        col.Item().Padding(10).Column(opCol =>
                        {
                            opCol.Item().Text("Cash Inflows:").FontSize(9).Bold().FontColor(SuccessColor);
                            foreach (var item in report.OperatingInflows)
                            {
                                opCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(item.Description).FontSize(9);
                                    row.ConstantItem(80).AlignRight().Text($"{item.Amount:N0}").FontSize(9).FontColor(SuccessColor);
                                });
                            }
                            opCol.Item().PaddingTop(5).Row(row =>
                            {
                                row.RelativeItem().Text("Total Cash Inflows").FontSize(9).Bold();
                                row.ConstantItem(80).AlignRight().Text($"{report.TotalOperatingInflows:N0}").FontSize(9).Bold().FontColor(SuccessColor);
                            });

                            opCol.Item().PaddingTop(10).Text("Cash Outflows:").FontSize(9).Bold().FontColor(ErrorColor);
                            foreach (var item in report.OperatingOutflows.Where(o => !o.Notes?.Contains("breakdown") ?? true))
                            {
                                opCol.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(item.Description).FontSize(9);
                                    row.ConstantItem(80).AlignRight().Text($"({item.Amount:N0})").FontSize(9).FontColor(ErrorColor);
                                });
                            }
                            opCol.Item().PaddingTop(5).Row(row =>
                            {
                                row.RelativeItem().Text("Total Cash Outflows").FontSize(9).Bold();
                                row.ConstantItem(80).AlignRight().Text($"({report.TotalOperatingOutflows:N0})").FontSize(9).Bold().FontColor(ErrorColor);
                            });
                        });

                        col.Item().Background(Color.FromHex("#E8F5E9")).Padding(8).Row(row =>
                        {
                            row.RelativeItem().Text("Net Cash from Operating Activities").FontSize(10).Bold();
                            row.ConstantItem(100).AlignRight().Text($"KES {report.NetCashFromOperations:N0}")
                                .FontSize(10).Bold().FontColor(report.NetCashFromOperations >= 0 ? SuccessColor : ErrorColor);
                        });
                    });

                    // Net Change
                    column.Item().Background(PrimaryColor).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Text("NET CHANGE IN CASH").FontSize(11).Bold().FontColor(Colors.White);
                        row.ConstantItem(120).AlignRight().Text($"KES {report.NetCashChange:N0}").FontSize(11).Bold().FontColor(Colors.White);
                    });

                    // Closing Balance
                    column.Item().Background(PrimaryDark).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Text("CLOSING CASH BALANCE").FontSize(12).Bold().FontColor(Colors.White);
                        row.ConstantItem(120).AlignRight().Text($"KES {report.ClosingCashBalance:N0}").FontSize(12).Bold().FontColor(Colors.White);
                    });
                });

                page.Footer().Element(ComposeFinancialFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportCashFlowToExcel(CashFlowReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Cash Flow");

        sheet.Cell(1, 1).Value = "CASH FLOW STATEMENT";
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = report.QuarryName;
        sheet.Cell(3, 1).Value = $"For Period: {report.PeriodStart:MMM dd} - {report.PeriodEnd:MMM dd, yyyy}";

        int row = 5;

        sheet.Cell(row, 1).Value = "Opening Cash Balance";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = report.OpeningCashBalance;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        row += 2;

        // Operating Activities
        sheet.Cell(row, 1).Value = "CASH FLOWS FROM OPERATING ACTIVITIES";
        sheet.Range(row, 1, row, 2).Merge().Style.Fill.BackgroundColor = HexToXLColor("#4CAF50");
        sheet.Range(row, 1, row, 2).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
        row++;

        sheet.Cell(row, 1).Value = "Cash Inflows:";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        foreach (var item in report.OperatingInflows)
        {
            sheet.Cell(row, 1).Value = item.Description;
            sheet.Cell(row, 2).Value = item.Amount;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
        }

        sheet.Cell(row, 1).Value = "Total Cash Inflows";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = report.TotalOperatingInflows;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        row += 2;

        sheet.Cell(row, 1).Value = "Cash Outflows:";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        foreach (var item in report.OperatingOutflows.Where(o => !o.Notes?.Contains("breakdown") ?? true))
        {
            sheet.Cell(row, 1).Value = item.Description;
            sheet.Cell(row, 2).Value = -item.Amount;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
        }

        sheet.Cell(row, 1).Value = "Total Cash Outflows";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = -report.TotalOperatingOutflows;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 2).Style.Font.Bold = true;
        row += 2;

        sheet.Cell(row, 1).Value = "Net Cash from Operating Activities";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = report.NetCashFromOperations;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = HexToXLColor("#E8F5E9");
        sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
        row += 2;

        sheet.Cell(row, 1).Value = "NET CHANGE IN CASH";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = report.NetCashChange;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = HexToXLColor("#1976D2");
        sheet.Range(row, 1, row, 2).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
        row += 2;

        sheet.Cell(row, 1).Value = "CLOSING CASH BALANCE";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = report.ClosingCashBalance;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = HexToXLColor("#0D47A1");
        sheet.Range(row, 1, row, 2).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 2).Style.Font.FontSize = 12;

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region AR Aging

    public byte[] ExportARAgingToPdf(ARAgingReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(TextPrimary));

                page.Header().Element(c => ComposeFinancialHeader(c, "ACCOUNTS RECEIVABLE AGING REPORT",
                    report.QuarryName, $"As of {report.AsOfDate:MMMM dd, yyyy}"));

                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Spacing(10);

                    // Summary
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Total Outstanding: KES {report.TotalOutstanding:N0}").Bold().FontSize(11).FontColor(ErrorColor);
                            c.Item().Text($"Customers: {report.CustomerCount} | Invoices: {report.InvoiceCount}").FontSize(9).FontColor(TextSecondary);
                        });
                    });

                    // Aging buckets summary
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(8).Column(c =>
                        {
                            c.Item().Text("Current (0 days)").FontSize(8).FontColor(TextSecondary);
                            c.Item().Text($"KES {report.TotalCurrent:N0}").Bold().FontColor(SuccessColor);
                            c.Item().Text($"{report.CurrentPercentage:N1}%").FontSize(8);
                        });
                        row.ConstantItem(5);
                        row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(8).Column(c =>
                        {
                            c.Item().Text("1-30 Days").FontSize(8).FontColor(TextSecondary);
                            c.Item().Text($"KES {report.Total1To30Days:N0}").Bold().FontColor(WarningColor);
                            c.Item().Text($"{report.Days1To30Percentage:N1}%").FontSize(8);
                        });
                        row.ConstantItem(5);
                        row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(8).Column(c =>
                        {
                            c.Item().Text("31-60 Days").FontSize(8).FontColor(TextSecondary);
                            c.Item().Text($"KES {report.Total31To60Days:N0}").Bold().FontColor(WarningColor);
                            c.Item().Text($"{report.Days31To60Percentage:N1}%").FontSize(8);
                        });
                        row.ConstantItem(5);
                        row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(8).Column(c =>
                        {
                            c.Item().Text("61-90 Days").FontSize(8).FontColor(TextSecondary);
                            c.Item().Text($"KES {report.Total61To90Days:N0}").Bold().FontColor(ErrorColor);
                            c.Item().Text($"{report.Days61To90Percentage:N1}%").FontSize(8);
                        });
                        row.ConstantItem(5);
                        row.RelativeItem().Border(1).BorderColor(ErrorColor).Padding(8).Column(c =>
                        {
                            c.Item().Text("Over 90 Days").FontSize(8).FontColor(TextSecondary);
                            c.Item().Text($"KES {report.TotalOver90Days:N0}").Bold().FontColor(ErrorColor);
                            c.Item().Text($"{report.Over90DaysPercentage:N1}%").FontSize(8);
                        });
                    });

                    // Customer table
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);    // Vehicle/Customer
                            columns.ConstantColumn(70);   // Current
                            columns.ConstantColumn(70);   // 1-30
                            columns.ConstantColumn(70);   // 31-60
                            columns.ConstantColumn(70);   // 61-90
                            columns.ConstantColumn(70);   // 90+
                            columns.ConstantColumn(80);   // Total
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(PrimaryColor).Padding(5).Text("CUSTOMER").FontSize(8).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).AlignRight().Text("CURRENT").FontSize(8).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).AlignRight().Text("1-30 DAYS").FontSize(8).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).AlignRight().Text("31-60 DAYS").FontSize(8).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).AlignRight().Text("61-90 DAYS").FontSize(8).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).AlignRight().Text("90+ DAYS").FontSize(8).Bold().FontColor(Colors.White);
                            header.Cell().Background(PrimaryColor).Padding(5).AlignRight().Text("TOTAL").FontSize(8).Bold().FontColor(Colors.White);
                        });

                        foreach (var customer in report.Customers)
                        {
                            var customerName = !string.IsNullOrEmpty(customer.ClientName)
                                ? $"{customer.VehicleRegistration} ({customer.ClientName})"
                                : customer.VehicleRegistration;

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(4).Text(customerName).FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(4).AlignRight()
                                .Text(customer.Current > 0 ? $"{customer.Current:N0}" : "-").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(4).AlignRight()
                                .Text(customer.Days1To30 > 0 ? $"{customer.Days1To30:N0}" : "-").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(4).AlignRight()
                                .Text(customer.Days31To60 > 0 ? $"{customer.Days31To60:N0}" : "-").FontSize(8);
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(4).AlignRight()
                                .Text(customer.Days61To90 > 0 ? $"{customer.Days61To90:N0}" : "-").FontSize(8).FontColor(customer.Days61To90 > 0 ? ErrorColor : TextPrimary);
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(4).AlignRight()
                                .Text(customer.Over90Days > 0 ? $"{customer.Over90Days:N0}" : "-").FontSize(8).FontColor(customer.Over90Days > 0 ? ErrorColor : TextPrimary);
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(4).AlignRight()
                                .Text($"{customer.TotalOutstanding:N0}").FontSize(8).Bold();
                        }

                        // Totals
                        table.Cell().Background(BackgroundLight).Padding(5).Text("TOTALS").FontSize(8).Bold();
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text($"{report.TotalCurrent:N0}").FontSize(8).Bold();
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text($"{report.Total1To30Days:N0}").FontSize(8).Bold();
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text($"{report.Total31To60Days:N0}").FontSize(8).Bold();
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text($"{report.Total61To90Days:N0}").FontSize(8).Bold().FontColor(ErrorColor);
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text($"{report.TotalOver90Days:N0}").FontSize(8).Bold().FontColor(ErrorColor);
                        table.Cell().Background(ErrorColor).Padding(5).AlignRight().Text($"{report.TotalOutstanding:N0}").FontSize(8).Bold().FontColor(Colors.White);
                    });
                });

                page.Footer().Element(ComposeFinancialFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportARAgingToExcel(ARAgingReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("AR Aging");

        sheet.Cell(1, 1).Value = "ACCOUNTS RECEIVABLE AGING REPORT";
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = report.QuarryName;
        sheet.Cell(3, 1).Value = $"As of {report.AsOfDate:MMMM dd, yyyy}";
        sheet.Cell(4, 1).Value = $"Total Outstanding: KES {report.TotalOutstanding:N0}";
        sheet.Cell(4, 1).Style.Font.Bold = true;

        int row = 6;

        // Headers
        sheet.Cell(row, 1).Value = "Customer";
        sheet.Cell(row, 2).Value = "Current";
        sheet.Cell(row, 3).Value = "1-30 Days";
        sheet.Cell(row, 4).Value = "31-60 Days";
        sheet.Cell(row, 5).Value = "61-90 Days";
        sheet.Cell(row, 6).Value = "90+ Days";
        sheet.Cell(row, 7).Value = "Total";
        sheet.Range(row, 1, row, 7).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = HexToXLColor("#1976D2");
        sheet.Range(row, 1, row, 7).Style.Font.FontColor = XLColor.White;

        row++;
        foreach (var customer in report.Customers)
        {
            var customerName = !string.IsNullOrEmpty(customer.ClientName)
                ? $"{customer.VehicleRegistration} ({customer.ClientName})"
                : customer.VehicleRegistration;

            sheet.Cell(row, 1).Value = customerName;
            sheet.Cell(row, 2).Value = customer.Current;
            sheet.Cell(row, 3).Value = customer.Days1To30;
            sheet.Cell(row, 4).Value = customer.Days31To60;
            sheet.Cell(row, 5).Value = customer.Days61To90;
            sheet.Cell(row, 6).Value = customer.Over90Days;
            sheet.Cell(row, 7).Value = customer.TotalOutstanding;

            sheet.Range(row, 2, row, 7).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 7).Style.Font.Bold = true;
            row++;
        }

        // Totals
        sheet.Cell(row, 1).Value = "TOTALS";
        sheet.Cell(row, 2).Value = report.TotalCurrent;
        sheet.Cell(row, 3).Value = report.Total1To30Days;
        sheet.Cell(row, 4).Value = report.Total31To60Days;
        sheet.Cell(row, 5).Value = report.Total61To90Days;
        sheet.Cell(row, 6).Value = report.TotalOver90Days;
        sheet.Cell(row, 7).Value = report.TotalOutstanding;
        sheet.Range(row, 1, row, 7).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = HexToXLColor("#FFEBEE");
        sheet.Range(row, 2, row, 7).Style.NumberFormat.Format = "#,##0";

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region AP Summary

    public byte[] ExportAPSummaryToPdf(APSummaryReport report)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextPrimary));

                page.Header().Element(c => ComposeFinancialHeader(c, "ACCOUNTS PAYABLE SUMMARY",
                    report.QuarryName, $"As of {report.AsOfDate:MMMM dd, yyyy}"));

                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(15);

                    // Summary
                    column.Item().Background(ErrorColor).Padding(12).Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL ACCOUNTS PAYABLE").FontSize(12).Bold().FontColor(Colors.White);
                        row.ConstantItem(150).AlignRight().Text($"KES {report.TotalAccountsPayable:N0}").FontSize(14).Bold().FontColor(Colors.White);
                    });

                    // Broker Commissions
                    if (report.BrokerPayables.Any())
                    {
                        column.Item().Border(1).BorderColor(BorderColor).Column(col =>
                        {
                            col.Item().Background(WarningColor).Padding(8)
                                .Text($"BROKER COMMISSIONS PAYABLE (KES {report.TotalBrokerCommissions:N0})").FontSize(10).Bold().FontColor(Colors.White);

                            col.Item().Padding(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);    // Broker Name
                                    columns.ConstantColumn(80);   // Phone
                                    columns.ConstantColumn(80);   // Period
                                    columns.ConstantColumn(60);   // Sales
                                    columns.ConstantColumn(80);   // Amount
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(BackgroundLight).Padding(5).Text("BROKER").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).Text("PHONE").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).Text("PERIOD").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("SALES").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("AMOUNT").FontSize(8).Bold();
                                });

                                foreach (var broker in report.BrokerPayables)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).Text(broker.BrokerName).FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).Text(broker.Phone ?? "").FontSize(8).FontColor(TextSecondary);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).Text(broker.Period).FontSize(8);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight().Text($"{broker.SalesCount}").FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight().Text($"{broker.AmountDue:N0}").FontSize(9).Bold();
                                }
                            });
                        });
                    }

                    // Accrued Fees
                    if (report.AccruedFees.Any())
                    {
                        column.Item().Border(1).BorderColor(BorderColor).Column(col =>
                        {
                            col.Item().Background(PrimaryColor).Padding(8)
                                .Text($"ACCRUED OPERATING FEES (KES {report.TotalAccruedFees:N0})").FontSize(10).Bold().FontColor(Colors.White);

                            col.Item().Padding(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(2);    // Fee Type
                                    columns.ConstantColumn(80);   // Period
                                    columns.ConstantColumn(60);   // Sales
                                    columns.ConstantColumn(60);   // Qty
                                    columns.ConstantColumn(50);   // Rate
                                    columns.ConstantColumn(80);   // Amount
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(BackgroundLight).Padding(5).Text("FEE TYPE").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).Text("PERIOD").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("SALES").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("QTY").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("RATE").FontSize(8).Bold();
                                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("AMOUNT").FontSize(8).Bold();
                                });

                                foreach (var fee in report.AccruedFees)
                                {
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).Text(fee.FeeType).FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).Text(fee.Period).FontSize(8);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight().Text($"{fee.SalesCount}").FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight().Text($"{fee.TotalQuantity:N0}").FontSize(9);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight().Text($"{fee.FeePerUnit:N0}").FontSize(8).FontColor(TextSecondary);
                                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight().Text($"{fee.AmountDue:N0}").FontSize(9).Bold();
                                }
                            });
                        });
                    }
                });

                page.Footer().Element(ComposeFinancialFooter);
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportAPSummaryToExcel(APSummaryReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("AP Summary");

        sheet.Cell(1, 1).Value = "ACCOUNTS PAYABLE SUMMARY";
        sheet.Cell(1, 1).Style.Font.FontSize = 16;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = report.QuarryName;
        sheet.Cell(3, 1).Value = $"As of {report.AsOfDate:MMMM dd, yyyy}";
        sheet.Cell(4, 1).Value = $"Total Payable: KES {report.TotalAccountsPayable:N0}";
        sheet.Cell(4, 1).Style.Font.Bold = true;

        int row = 6;

        // Broker Commissions
        if (report.BrokerPayables.Any())
        {
            sheet.Cell(row, 1).Value = "BROKER COMMISSIONS PAYABLE";
            sheet.Range(row, 1, row, 5).Merge().Style.Fill.BackgroundColor = HexToXLColor("#FF9800");
            sheet.Range(row, 1, row, 5).Style.Font.FontColor = XLColor.White;
            sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
            row++;

            sheet.Cell(row, 1).Value = "Broker Name";
            sheet.Cell(row, 2).Value = "Phone";
            sheet.Cell(row, 3).Value = "Period";
            sheet.Cell(row, 4).Value = "Sales";
            sheet.Cell(row, 5).Value = "Amount Due";
            sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = HexToXLColor("#FFF3E0");
            row++;

            foreach (var broker in report.BrokerPayables)
            {
                sheet.Cell(row, 1).Value = broker.BrokerName;
                sheet.Cell(row, 2).Value = broker.Phone ?? "";
                sheet.Cell(row, 3).Value = broker.Period;
                sheet.Cell(row, 4).Value = broker.SalesCount;
                sheet.Cell(row, 5).Value = broker.AmountDue;
                sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            sheet.Cell(row, 4).Value = "Total:";
            sheet.Cell(row, 4).Style.Font.Bold = true;
            sheet.Cell(row, 5).Value = report.TotalBrokerCommissions;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 5).Style.Font.Bold = true;
            row += 2;
        }

        // Accrued Fees
        if (report.AccruedFees.Any())
        {
            sheet.Cell(row, 1).Value = "ACCRUED OPERATING FEES";
            sheet.Range(row, 1, row, 5).Merge().Style.Fill.BackgroundColor = HexToXLColor("#1976D2");
            sheet.Range(row, 1, row, 5).Style.Font.FontColor = XLColor.White;
            sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
            row++;

            sheet.Cell(row, 1).Value = "Fee Type";
            sheet.Cell(row, 2).Value = "Period";
            sheet.Cell(row, 3).Value = "Quantity";
            sheet.Cell(row, 4).Value = "Rate";
            sheet.Cell(row, 5).Value = "Amount Due";
            sheet.Range(row, 1, row, 5).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 5).Style.Fill.BackgroundColor = HexToXLColor("#E3F2FD");
            row++;

            foreach (var fee in report.AccruedFees)
            {
                sheet.Cell(row, 1).Value = fee.FeeType;
                sheet.Cell(row, 2).Value = fee.Period;
                sheet.Cell(row, 3).Value = fee.TotalQuantity;
                sheet.Cell(row, 4).Value = fee.FeePerUnit;
                sheet.Cell(row, 5).Value = fee.AmountDue;
                sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            sheet.Cell(row, 4).Value = "Total:";
            sheet.Cell(row, 4).Style.Font.Bold = true;
            sheet.Cell(row, 5).Value = report.TotalAccruedFees;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 5).Style.Font.Bold = true;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #endregion

    #region Combined Package

    public byte[] ExportFinancialPackageToExcel(
        TrialBalanceReport trialBalance,
        ProfitLossReport profitLoss,
        BalanceSheetReport balanceSheet,
        CashFlowReport cashFlow,
        ARAgingReport arAging,
        APSummaryReport apSummary)
    {
        using var workbook = new XLWorkbook();

        // Add each report as a separate worksheet
        AddTrialBalanceSheet(workbook, trialBalance);
        AddProfitLossSheet(workbook, profitLoss);
        AddBalanceSheetSheet(workbook, balanceSheet);
        AddCashFlowSheet(workbook, cashFlow);
        AddARAgingSheet(workbook, arAging);
        AddAPSummarySheet(workbook, apSummary);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddTrialBalanceSheet(XLWorkbook workbook, TrialBalanceReport report)
    {
        var sheet = workbook.Worksheets.Add("Trial Balance");
        // Use existing Excel export logic
        var tempExcel = ExportTrialBalanceToExcel(report);
        using var tempWorkbook = new XLWorkbook(new MemoryStream(tempExcel));
        tempWorkbook.Worksheets.First().CopyTo(workbook, "Trial Balance");
        workbook.Worksheets.Worksheet("Trial Balance1").Delete();
    }

    private void AddProfitLossSheet(XLWorkbook workbook, ProfitLossReport report)
    {
        var tempExcel = ExportProfitLossToExcel(report);
        using var tempWorkbook = new XLWorkbook(new MemoryStream(tempExcel));
        var sourceSheet = tempWorkbook.Worksheets.First();

        var targetSheet = workbook.Worksheets.Add("Profit & Loss");
        foreach (var cell in sourceSheet.CellsUsed())
        {
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Value = cell.Value;
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Style = cell.Style;
        }
        targetSheet.Columns().AdjustToContents();
    }

    private void AddBalanceSheetSheet(XLWorkbook workbook, BalanceSheetReport report)
    {
        var tempExcel = ExportBalanceSheetToExcel(report);
        using var tempWorkbook = new XLWorkbook(new MemoryStream(tempExcel));
        var sourceSheet = tempWorkbook.Worksheets.First();

        var targetSheet = workbook.Worksheets.Add("Balance Sheet");
        foreach (var cell in sourceSheet.CellsUsed())
        {
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Value = cell.Value;
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Style = cell.Style;
        }
        targetSheet.Columns().AdjustToContents();
    }

    private void AddCashFlowSheet(XLWorkbook workbook, CashFlowReport report)
    {
        var tempExcel = ExportCashFlowToExcel(report);
        using var tempWorkbook = new XLWorkbook(new MemoryStream(tempExcel));
        var sourceSheet = tempWorkbook.Worksheets.First();

        var targetSheet = workbook.Worksheets.Add("Cash Flow");
        foreach (var cell in sourceSheet.CellsUsed())
        {
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Value = cell.Value;
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Style = cell.Style;
        }
        targetSheet.Columns().AdjustToContents();
    }

    private void AddARAgingSheet(XLWorkbook workbook, ARAgingReport report)
    {
        var tempExcel = ExportARAgingToExcel(report);
        using var tempWorkbook = new XLWorkbook(new MemoryStream(tempExcel));
        var sourceSheet = tempWorkbook.Worksheets.First();

        var targetSheet = workbook.Worksheets.Add("AR Aging");
        foreach (var cell in sourceSheet.CellsUsed())
        {
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Value = cell.Value;
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Style = cell.Style;
        }
        targetSheet.Columns().AdjustToContents();
    }

    private void AddAPSummarySheet(XLWorkbook workbook, APSummaryReport report)
    {
        var tempExcel = ExportAPSummaryToExcel(report);
        using var tempWorkbook = new XLWorkbook(new MemoryStream(tempExcel));
        var sourceSheet = tempWorkbook.Worksheets.First();

        var targetSheet = workbook.Worksheets.Add("AP Summary");
        foreach (var cell in sourceSheet.CellsUsed())
        {
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Value = cell.Value;
            targetSheet.Cell(cell.Address.RowNumber, cell.Address.ColumnNumber).Style = cell.Style;
        }
        targetSheet.Columns().AdjustToContents();
    }

    #endregion

    #region Helper Methods

    private void ComposeFinancialHeader(IContainer container, string title, string quarryName, string period)
    {
        container.Column(col =>
        {
            col.Item().Height(6).Background(PrimaryColor);

            col.Item().PaddingVertical(15).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Row(logoRow =>
                    {
                        logoRow.AutoItem().Height(36).Width(36)
                            .Background(PrimaryColor)
                            .AlignCenter()
                            .AlignMiddle()
                            .Text("QD")
                            .FontSize(14)
                            .Bold()
                            .FontColor(Colors.White);

                        logoRow.AutoItem().PaddingLeft(10).AlignBottom().Column(titleCol =>
                        {
                            titleCol.Item().Text("QDesk-Pro")
                                .Bold()
                                .FontSize(18)
                                .FontColor(PrimaryDark);
                            titleCol.Item().Text("Quarry Management System")
                                .FontSize(7)
                                .FontColor(TextSecondary)
                                .Italic();
                        });
                    });

                    column.Item().PaddingTop(10)
                        .Text(title)
                        .FontSize(14)
                        .Bold()
                        .LetterSpacing(0.5f)
                        .FontColor(TextPrimary);

                    column.Item()
                        .Text($"{quarryName} • {period}")
                        .FontSize(9)
                        .FontColor(TextSecondary);
                });

                row.ConstantItem(120).AlignRight().Column(dateCol =>
                {
                    dateCol.Item().AlignRight().Text("Report Generated")
                        .FontSize(8)
                        .FontColor(TextSecondary);
                    dateCol.Item().AlignRight().Text(DateTime.Now.ToString("MMM dd, yyyy"))
                        .Bold()
                        .FontSize(10);
                    dateCol.Item().AlignRight().Text(DateTime.Now.ToString("hh:mm tt"))
                        .FontSize(8)
                        .FontColor(TextSecondary);
                });
            });

            col.Item().Height(2).Background(PrimaryColor);
        });
    }

    private void ComposeFinancialFooter(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Height(1).Background(BorderColor);

            col.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Generated by ").FontSize(8).FontColor(TextSecondary);
                    text.Span("QDeskPro").Bold().FontSize(8).FontColor(PrimaryColor);
                    text.Span(" • Financial Reports").FontSize(8).FontColor(TextSecondary);
                });

                row.RelativeItem().AlignCenter().Text(DateTime.Now.ToString("MMMM dd, yyyy"))
                    .FontSize(8).FontColor(TextSecondary);

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span("Page ").FontSize(8).FontColor(TextSecondary);
                    text.CurrentPageNumber().FontSize(8).Bold();
                    text.Span(" of ").FontSize(8).FontColor(TextSecondary);
                    text.TotalPages().FontSize(8).Bold();
                });
            });
        });
    }

    private void ComposePnLSection(IContainer container, string title, List<ProfitLossLineItem> items, double total, Color headerColor, bool isRevenue)
    {
        container.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Background(headerColor).Padding(8)
                .Text(title).FontSize(10).Bold().FontColor(Colors.White);

            col.Item().Padding(10).Column(itemsCol =>
            {
                foreach (var item in items)
                {
                    itemsCol.Item().Row(row =>
                    {
                        row.ConstantItem(50).Text(item.AccountCode).FontSize(8).FontColor(TextSecondary);
                        row.RelativeItem().Text(item.Description).FontSize(9);
                        row.ConstantItem(80).AlignRight().Text($"{item.Amount:N0}").FontSize(9);
                    });
                }
            });

            col.Item().Background(BackgroundLight).Padding(8).Row(row =>
            {
                row.RelativeItem().Text($"Total {title}").FontSize(9).Bold();
                row.ConstantItem(80).AlignRight().Text($"KES {total:N0}").FontSize(9).Bold().FontColor(headerColor);
            });
        });
    }

    private static XLColor HexToXLColor(string hex)
    {
        hex = hex.TrimStart('#');
        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);
        return XLColor.FromArgb(r, g, b);
    }

    #endregion
}
