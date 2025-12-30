using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using QDeskPro.Domain.Entities;
using System.Text;

namespace QDeskPro.Features.Reports.Services;

/// <summary>
/// Service for exporting sales reports in various formats (Markdown, PDF, Excel)
/// </summary>
public class ReportExportService
{
    // Brand colors using QuestPDF Color type
    private static readonly Color PrimaryColor = Color.FromHex("#1976D2");      // Blue
    private static readonly Color PrimaryDark = Color.FromHex("#0D47A1");       // Dark Blue
    private static readonly Color SuccessColor = Color.FromHex("#4CAF50");      // Green
    private static readonly Color SuccessLight = Color.FromHex("#E8F5E9");      // Light Green
    private static readonly Color ErrorColor = Color.FromHex("#F44336");        // Red
    private static readonly Color ErrorLight = Color.FromHex("#FFEBEE");        // Light Red
    private static readonly Color WarningColor = Color.FromHex("#FF9800");      // Orange
    private static readonly Color TextPrimary = Color.FromHex("#212121");       // Dark text
    private static readonly Color TextSecondary = Color.FromHex("#757575");     // Gray text
    private static readonly Color BackgroundLight = Color.FromHex("#FAFAFA");   // Light background
    private static readonly Color BorderColor = Color.FromHex("#E0E0E0");       // Border gray

    static ReportExportService()
    {
        // Configure QuestPDF license (Community license for open source)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Generate markdown formatted text for WhatsApp sharing
    /// </summary>
    public string GenerateMarkdownReport(ClerkReportData report, string? dailyNotes = null, string? pdfDownloadUrl = null)
    {
        var sb = new StringBuilder();

        // Title
        sb.AppendLine($"*SALES REPORT - {report.ReportTitle}*");
        sb.AppendLine($"_{report.QuarryName} • {report.ClerkName}_");
        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        // Summary
        sb.AppendLine("*SUMMARY*");
        sb.AppendLine();
        if (report.IsSingleDay)
            sb.AppendLine($"Opening Balance (B/F): *KES {report.OpeningBalance:N0}*");
        sb.AppendLine($"Quantity: *{report.TotalQuantity:N0} pcs*");
        sb.AppendLine($"Sales: *KES {report.TotalSales:N0}*");

        // Calculate other expenses (user expenses only)
        var otherExpensesTotal = report.ExpenseItems.Where(e => e.LineType == "User Expense").Sum(e => e.Amount);

        sb.AppendLine($"Total Expenses: *KES {report.TotalExpenses:N0}*");
        sb.AppendLine($"  • Commissions: KES {report.Commission:N0}");
        sb.AppendLine($"  • Loaders Fee: KES {report.LoadersFee:N0}");
        if (report.LandRateVisible)
            sb.AppendLine($"  • Land Rate: KES {report.LandRateFee:N0}");
        if (otherExpensesTotal > 0)
            sb.AppendLine($"  • Other Expenses: KES {otherExpensesTotal:N0}");
        sb.AppendLine($"Earnings: *KES {report.Earnings:N0}*");
        if (report.UnpaidOrders)
            sb.AppendLine($"Unpaid Orders: *KES {report.Unpaid:N0}*");
        sb.AppendLine($"Net Income: *KES {report.NetEarnings:N0}*");
        sb.AppendLine($"Banked: *KES {report.Banked:N0}*");
        sb.AppendLine($"*Closing Balance (C/H): KES {report.CashInHand:N0}*");
        sb.AppendLine();

        // Daily Notes if provided
        if (!string.IsNullOrWhiteSpace(dailyNotes))
        {
            sb.AppendLine($"_Notes: {dailyNotes}_");
            sb.AppendLine();
        }

        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        // Sales
        sb.AppendLine($"*SALES ({report.Sales.Count} orders)*");
        sb.AppendLine();

        // Group sales by product
        var salesByProduct = report.Sales
            .GroupBy(s => s.Product?.ProductName ?? "Unknown")
            .Select(g => new
            {
                Product = g.Key,
                Quantity = g.Sum(s => s.Quantity),
                Amount = g.Sum(s => s.GrossSaleAmount),
                Sales = g.ToList()
            })
            .OrderByDescending(g => g.Amount);

        foreach (var group in salesByProduct)
        {
            sb.AppendLine($"*{group.Product}* - {group.Quantity:N0}pcs: KES {group.Amount:N0}");
            foreach (var sale in group.Sales)
            {
                var lineTotal = sale.Quantity * sale.PricePerUnit;
                var saleText = $"  {sale.SaleDate:dd/MM} • {sale.VehicleRegistration} - {sale.Quantity:N0} × KES {sale.PricePerUnit:N1} = {lineTotal:N1}";

                // Strikethrough unpaid orders
                if (sale.PaymentStatus != "Paid")
                {
                    sb.AppendLine($"  ~{saleText}~ (Unpaid)");
                }
                else
                {
                    sb.AppendLine($"  {saleText}");
                }
            }
            sb.AppendLine();
        }

        // Sub Total sales
        sb.AppendLine($"*Sub Total: KES {report.TotalSales:N0}*");
        sb.AppendLine();

        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();

        // Expenses - Grouped
        sb.AppendLine($"*EXPENSES ({report.ExpenseItems.Count} items)*");
        sb.AppendLine();

        // Group expenses by type
        var commissionTotal = report.ExpenseItems.Where(e => e.LineType == "Commission Expense").Sum(e => e.Amount);
        var loadersFeeTotal = report.ExpenseItems.Where(e => e.LineType == "Loaders Fee Expense").Sum(e => e.Amount);
        var landRateTotal = report.ExpenseItems.Where(e => e.LineType == "Land Rate Fee Expense").Sum(e => e.Amount);
        var userExpenses = report.ExpenseItems.Where(e => e.LineType == "User Expense").ToList();

        if (commissionTotal > 0)
            sb.AppendLine($"*Sale Commissions:* KES {commissionTotal:N0}");
        if (loadersFeeTotal > 0)
            sb.AppendLine($"*Loaders Fees:* KES {loadersFeeTotal:N0}");
        if (landRateTotal > 0)
            sb.AppendLine($"*Land Rate Fees:* KES {landRateTotal:N0}");

        if (userExpenses.Any())
        {
            sb.AppendLine();
            sb.AppendLine("*Other Expenses:*");
            foreach (var expense in userExpenses)
            {
                sb.AppendLine($"  {expense.ItemDate:dd/MM} • {expense.LineItem}: KES {expense.Amount:N0}");
            }
        }

        // Total expenses footer
        sb.AppendLine();
        sb.AppendLine($"*Total Expenses: KES {report.TotalExpenses:N0}*");

        // Fuel Usage
        if (report.FuelUsages.Any())
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();
            sb.AppendLine($"*FUEL USAGE ({report.FuelUsages.Count} records)*");
            sb.AppendLine();
            foreach (var fuel in report.FuelUsages)
            {
                sb.AppendLine($"{fuel.UsageDate:dd/MM} • Old: {fuel.OldStock:N1}L | New: {fuel.NewStock:N1}L | Used: {fuel.Used:N1}L | Bal: {fuel.Balance:N1}L");
            }
        }

        // Banking
        if (report.Bankings.Any())
        {
            sb.AppendLine();
            sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            sb.AppendLine();
            sb.AppendLine($"*BANKING ({report.Bankings.Count} records)*");
            sb.AppendLine();
            foreach (var banking in report.Bankings)
            {
                sb.AppendLine($"{banking.BankingDate:dd/MM} • {banking.Item}: KES {banking.AmountBanked:N0}");
                if (!string.IsNullOrWhiteSpace(banking.RefCode))
                    sb.AppendLine($"  Ref: {banking.RefCode}");
            }
        }

        // Footer with PDF link
        sb.AppendLine();
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        sb.AppendLine($"_Generated on {DateTime.Now:dd/MM/yyyy hh:mm tt}_");

        if (!string.IsNullOrWhiteSpace(pdfDownloadUrl))
        {
            sb.AppendLine();
            sb.AppendLine("📄 *Download Detailed PDF Report*");
            sb.AppendLine();
            sb.AppendLine(pdfDownloadUrl);
        }

        sb.AppendLine();
        sb.AppendLine("_Powered by QDeskPro • Quarry Management System_");

        return sb.ToString();
    }

    /// <summary>
    /// Generate professionally formatted PDF report
    /// </summary>
    public byte[] GeneratePdfReport(ClerkReportData report, string? dailyNotes = null)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextPrimary));

                // Header
                page.Header().Element(c => ComposeHeader(c, report));

                page.Content().PaddingVertical(15).Element(container =>
                {
                    container.Column(column =>
                    {
                        column.Spacing(20);

                        // Report Info Card
                        column.Item().Element(c => ComposeReportInfo(c, report));

                        // Summary Section
                        column.Item().Element(c => ComposeSummary(c, report, dailyNotes));

                        // Sales Section
                        column.Item().Element(c => ComposeSalesTable(c, report));

                        // Expenses Section
                        column.Item().Element(c => ComposeExpensesTable(c, report));

                        // Fuel Usage Section (if any)
                        if (report.FuelUsages.Any())
                        {
                            column.Item().Element(c => ComposeFuelUsageTable(c, report));
                        }

                        // Banking Section (if any)
                        if (report.Bankings.Any())
                        {
                            column.Item().Element(c => ComposeBankingTable(c, report));
                        }
                    });
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>
    /// Generate professionally formatted Excel report
    /// </summary>
    public byte[] GenerateExcelReport(ClerkReportData report, string? dailyNotes = null)
    {
        using var workbook = new XLWorkbook();

        // Summary Sheet
        var summarySheet = workbook.Worksheets.Add("Summary");
        ComposeSummarySheet(summarySheet, report, dailyNotes);

        // Sales Sheet
        var salesSheet = workbook.Worksheets.Add("Sales");
        ComposeSalesSheet(salesSheet, report);

        // Expenses Sheet
        var expensesSheet = workbook.Worksheets.Add("Expenses");
        ComposeExpensesSheet(expensesSheet, report);

        // Fuel Usage Sheet (if any)
        if (report.FuelUsages.Any())
        {
            var fuelSheet = workbook.Worksheets.Add("Fuel Usage");
            ComposeFuelUsageSheet(fuelSheet, report);
        }

        // Banking Sheet (if any)
        if (report.Bankings.Any())
        {
            var bankingSheet = workbook.Worksheets.Add("Banking");
            ComposeBankingSheet(bankingSheet, report);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    #region PDF Composition Methods

    private void ComposeHeader(IContainer container, ClerkReportData report)
    {
        container.Column(col =>
        {
            // Top accent bar with gradient effect
            col.Item().Height(6).Background(PrimaryColor);

            col.Item().PaddingVertical(15).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Row(logoRow =>
                    {
                        // Modern app logo
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
                                .FontSize(22)
                                .FontColor(PrimaryDark);

                            titleCol.Item().Text("Quarry Management System")
                                .FontSize(7)
                                .FontColor(TextSecondary)
                                .Italic();
                        });
                    });

                    column.Item().PaddingTop(10)
                        .Text("SALES REPORT")
                        .FontSize(16)
                        .Bold()
                        .LetterSpacing(1)
                        .FontColor(TextPrimary);

                    column.Item()
                        .Text(report.ReportTitle)
                        .FontSize(10)
                        .FontColor(TextSecondary);
                });

                row.ConstantItem(140).AlignRight().Column(dateCol =>
                {
                    dateCol.Item().AlignRight().Text("Report Generated")
                        .FontSize(8)
                        .FontColor(TextSecondary);
                    dateCol.Item().AlignRight().Text(DateTime.Now.ToString("MMMM dd, yyyy"))
                        .Bold()
                        .FontSize(11);
                    dateCol.Item().AlignRight().Text(DateTime.Now.ToString("hh:mm tt"))
                        .FontSize(9)
                        .FontColor(TextSecondary);
                });
            });

            // Bottom separator line with shadow effect
            col.Item().Height(2).Background(PrimaryColor);
        });
    }

    private void ComposeReportInfo(IContainer container, ClerkReportData report)
    {
        container.Border(1).BorderColor(BorderColor).Column(col =>
        {
            col.Item().Background(PrimaryColor).Padding(10)
                .Text("REPORT DETAILS")
                .FontSize(10)
                .Bold()
                .FontColor(Colors.White);

            col.Item().Background(Colors.White).Padding(15).Row(row =>
            {
                // Quarry Info
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Quarry").FontSize(8).FontColor(TextSecondary);
                    c.Item().PaddingTop(3).Text(report.QuarryName).Bold().FontSize(13).FontColor(PrimaryDark);
                });

                row.ConstantItem(10);

                // Clerk Info
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Prepared By").FontSize(8).FontColor(TextSecondary);
                    c.Item().PaddingTop(3).Text(report.ClerkName).Bold().FontSize(13);
                });

                row.ConstantItem(10);

                // Date Range
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Period").FontSize(8).FontColor(TextSecondary);
                    c.Item().PaddingTop(3).Text(report.ReportTitle).Bold().FontSize(11);
                });
            });
        });
    }

    private void ComposeSummary(IContainer container, ClerkReportData report, string? dailyNotes)
    {
        // Calculate financial ratios
        var profitMargin = report.TotalSales > 0 ? (report.Earnings / report.TotalSales) * 100 : 0;
        var expenseRatio = report.TotalSales > 0 ? (report.TotalExpenses / report.TotalSales) * 100 : 0;
        var bankingRate = report.Earnings > 0 ? (report.Banked / report.Earnings) * 100 : 0;

        container.Border(1).BorderColor(BorderColor).Column(col =>
        {
            // Modern header accent bar
            col.Item().Height(8).Background(PrimaryColor);

            col.Item().Background(PrimaryColor).Padding(12)
                .Row(row =>
                {
                    row.RelativeItem().Text("FINANCIAL SUMMARY")
                        .FontSize(11)
                        .Bold()
                        .LetterSpacing(0.5f)
                        .FontColor(Colors.White);

                    row.AutoItem().Text($"Period: {report.ReportTitle}")
                        .FontSize(8)
                        .FontColor(Colors.White)
                        .Italic();
                });

            col.Item().Background(Colors.White).Padding(15).Column(column =>
            {
                // ROW 1: Key Performance Metrics with Ratios
                column.Item().Row(row =>
                {
                    // Total Sales Card with icon
                    row.RelativeItem().Border(2).BorderColor(SuccessColor).Padding(12).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem().PaddingRight(6).Text("💰").FontSize(16);
                            r.RelativeItem().Text("TOTAL SALES").FontSize(8).Bold().FontColor(SuccessColor);
                        });
                        c.Item().PaddingTop(4).Text($"KES {report.TotalSales:N0}").Bold().FontSize(16).FontColor(SuccessColor);
                        c.Item().PaddingTop(2).Text($"{report.TotalQuantity:N0} pieces sold").FontSize(7).FontColor(TextSecondary);
                    });

                    row.ConstantItem(10);

                    // Total Expenses Card with icon
                    row.RelativeItem().Border(2).BorderColor(ErrorColor).Padding(12).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem().PaddingRight(6).Text("📊").FontSize(16);
                            r.RelativeItem().Text("TOTAL EXPENSES").FontSize(8).Bold().FontColor(ErrorColor);
                        });
                        c.Item().PaddingTop(4).Text($"KES {report.TotalExpenses:N0}").Bold().FontSize(16).FontColor(ErrorColor);
                        c.Item().PaddingTop(2).Text($"{expenseRatio:N1}% of sales").FontSize(7).FontColor(TextSecondary);
                    });

                    row.ConstantItem(10);

                    // Profit Margin Card - Highlighted
                    row.RelativeItem().Background(profitMargin >= 40 ? SuccessColor : (profitMargin >= 20 ? WarningColor : ErrorColor))
                        .Padding(12).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem().PaddingRight(6).Text("📈").FontSize(16);
                            r.RelativeItem().Text("PROFIT MARGIN").FontSize(8).Bold().FontColor(Colors.White);
                        });
                        c.Item().PaddingTop(4).Text($"{profitMargin:N1}%").Bold().FontSize(20).FontColor(Colors.White);
                        c.Item().PaddingTop(2).Text(profitMargin >= 40 ? "Excellent" : (profitMargin >= 20 ? "Good" : "Low"))
                            .FontSize(7).FontColor(Colors.White).Italic();
                    });
                });

                // ROW 2: BANKED - PRIMARY FOCAL POINT (Extra Large Card)
                column.Item().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem(3).Border(4).BorderColor(PrimaryDark).Background(Color.FromHex("#E3F2FD"))
                        .Padding(18).Column(c =>
                    {
                        c.Item().Row(r =>
                        {
                            r.AutoItem().PaddingRight(8).Text("🏦").FontSize(24);
                            r.RelativeItem().Column(labelCol =>
                            {
                                labelCol.Item().Text("AMOUNT BANKED").FontSize(9).Bold().FontColor(PrimaryDark).LetterSpacing(0.5f);
                                labelCol.Item().Text("Primary Focus Metric").FontSize(7).FontColor(PrimaryColor).Italic();
                            });
                            r.AutoItem().AlignRight().Text("⭐").FontSize(20);
                        });

                        c.Item().PaddingTop(8).Text($"KES {report.Banked:N0}")
                            .Bold()
                            .FontSize(24)
                            .FontColor(PrimaryDark);

                        // Banking rate progress indicator
                        c.Item().PaddingTop(8).Column(progressCol =>
                        {
                            progressCol.Item().Row(progressRow =>
                            {
                                progressRow.RelativeItem().Text($"Banking Rate: {bankingRate:N1}% of Earnings")
                                    .FontSize(7).FontColor(TextSecondary);
                            });

                            // Progress bar using Row
                            progressCol.Item().PaddingTop(3).Height(6).Border(1).BorderColor(Color.FromHex("#E0E0E0")).Row(progressRow =>
                            {
                                var progressPercent = Math.Min(bankingRate, 100);
                                var barColor = bankingRate >= 80 ? SuccessColor : (bankingRate >= 50 ? WarningColor : ErrorColor);

                                if (progressPercent > 0)
                                {
                                    progressRow.RelativeItem((float)progressPercent).Background(barColor);
                                }
                                if (progressPercent < 100)
                                {
                                    progressRow.RelativeItem((float)(100 - progressPercent)).Background(Color.FromHex("#F5F5F5"));
                                }
                            });
                        });

                        if (report.IsSingleDay)
                        {
                            c.Item().PaddingTop(6).Text($"Opening Balance: KES {report.OpeningBalance:N0}")
                                .FontSize(7).FontColor(TextSecondary);
                        }
                    });

                    row.ConstantItem(10);

                    // Supporting metrics column
                    row.RelativeItem(2).Column(supportCol =>
                    {
                        // Net Income
                        supportCol.Item().Border(1).BorderColor(BorderColor).Padding(10).Column(c =>
                        {
                            c.Item().Text("NET INCOME").FontSize(7).FontColor(TextSecondary);
                            c.Item().PaddingTop(3).Text($"KES {report.NetEarnings:N0}").Bold().FontSize(14).FontColor(SuccessColor);
                        });

                        supportCol.Item().PaddingTop(8);

                        // Earnings breakdown
                        supportCol.Item().Border(1).BorderColor(BorderColor).Padding(10).Column(c =>
                        {
                            c.Item().Text("EARNINGS").FontSize(7).FontColor(TextSecondary);
                            c.Item().PaddingTop(3).Text($"KES {report.Earnings:N0}").Bold().FontSize(14);

                            if (report.UnpaidOrders)
                            {
                                c.Item().PaddingTop(4).Row(r =>
                                {
                                    r.AutoItem().Text("⚠️").FontSize(10);
                                    r.AutoItem().PaddingLeft(4).Column(warningCol =>
                                    {
                                        warningCol.Item().Text("Unpaid Orders").FontSize(6).FontColor(ErrorColor);
                                        warningCol.Item().Text($"KES {report.Unpaid:N0}").FontSize(8).Bold().FontColor(ErrorColor);
                                    });
                                });
                            }
                        });

                        supportCol.Item().PaddingTop(8);

                        // Closing Balance (C/H) - Reduced prominence
                        supportCol.Item().Background(Color.FromHex("#F5F5F5")).Border(1).BorderColor(BorderColor)
                            .Padding(10).Column(c =>
                        {
                            c.Item().Text("CLOSING BALANCE (C/H)").FontSize(6).FontColor(TextSecondary);
                            c.Item().PaddingTop(3).Text($"KES {report.CashInHand:N0}").Bold().FontSize(11);
                            c.Item().PaddingTop(2).Text("Net Income - Banked").FontSize(5).FontColor(TextSecondary).Italic();
                        });
                    });
                });

                // ROW 3: Expense Breakdown Cards (Compact)
                column.Item().PaddingTop(12).Row(row =>
                {
                    row.RelativeItem().Border(1).BorderColor(Color.FromHex("#FFB74D")).Padding(8).Column(c =>
                    {
                        c.Item().Text("Commissions").FontSize(7).FontColor(TextSecondary);
                        c.Item().PaddingTop(2).Text($"KES {report.Commission:N0}").Bold().FontSize(11).FontColor(WarningColor);
                    });

                    row.ConstantItem(8);

                    row.RelativeItem().Border(1).BorderColor(Color.FromHex("#64B5F6")).Padding(8).Column(c =>
                    {
                        c.Item().Text("Loaders Fee").FontSize(7).FontColor(TextSecondary);
                        c.Item().PaddingTop(2).Text($"KES {report.LoadersFee:N0}").Bold().FontSize(11).FontColor(PrimaryColor);
                    });

                    row.ConstantItem(8);

                    if (report.LandRateVisible)
                    {
                        row.RelativeItem().Border(1).BorderColor(Color.FromHex("#81C784")).Padding(8).Column(c =>
                        {
                            c.Item().Text("Land Rate").FontSize(7).FontColor(TextSecondary);
                            c.Item().PaddingTop(2).Text($"KES {report.LandRateFee:N0}").Bold().FontSize(11).FontColor(SuccessColor);
                        });

                        row.ConstantItem(8);
                    }

                    row.RelativeItem().Border(1).BorderColor(BorderColor).Padding(8).Column(c =>
                    {
                        c.Item().Text("Other Expenses").FontSize(7).FontColor(TextSecondary);
                        var otherExpenses = report.TotalExpenses - report.Commission - report.LoadersFee - report.LandRateFee;
                        c.Item().PaddingTop(2).Text($"KES {otherExpenses:N0}").Bold().FontSize(11);
                    });
                });

                // Daily Notes (if provided)
                if (!string.IsNullOrWhiteSpace(dailyNotes))
                {
                    column.Item().PaddingTop(12)
                        .Border(1)
                        .BorderColor(Color.FromHex("#90CAF9"))
                        .Background(Color.FromHex("#E3F2FD"))
                        .Padding(10)
                        .Column(notesCol =>
                        {
                            notesCol.Item().Row(r =>
                            {
                                r.AutoItem().Text("📝").FontSize(12);
                                r.AutoItem().PaddingLeft(6).Text("DAILY NOTES").FontSize(8).Bold().FontColor(PrimaryDark);
                            });
                            notesCol.Item().PaddingTop(5).Text(dailyNotes).FontSize(8).FontColor(TextPrimary);
                        });
                }
            });
        });
    }

    private void ComposeSalesTable(IContainer container, ClerkReportData report)
    {
        container.Border(1).BorderColor(BorderColor).Column(column =>
        {
            column.Item().Background(SuccessColor).Padding(10)
                .Text($"SALES BREAKDOWN ({report.Sales.Count} orders)")
                .FontSize(10)
                .Bold()
                .FontColor(Colors.White);

            column.Item().Background(Colors.White).Padding(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(50);   // Date
                    columns.ConstantColumn(70);   // Vehicle
                    columns.RelativeColumn(2);    // Product
                    columns.ConstantColumn(50);   // Quantity
                    columns.ConstantColumn(55);   // Price
                    columns.ConstantColumn(70);   // Amount
                    columns.ConstantColumn(50);   // Status
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background(BackgroundLight).Padding(5).Text("DATE").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).Text("VEHICLE").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).Text("PRODUCT").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("QTY").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("PRICE").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("AMOUNT").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignCenter().Text("STATUS").FontSize(8).Bold();
                });

                // Data rows
                foreach (var sale in report.Sales.OrderBy(s => s.SaleDate))
                {
                    var isPaid = sale.PaymentStatus == "Paid";
                    var bgColor = isPaid ? Colors.White : ErrorLight;

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(sale.SaleDate?.ToString("dd/MM/yy") ?? "").FontSize(9);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(sale.VehicleRegistration).FontSize(9).Bold();

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(sale.Product?.ProductName ?? "").FontSize(9);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"{sale.Quantity:N0}").FontSize(9);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"{sale.PricePerUnit:N1}").FontSize(9);

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"{sale.GrossSaleAmount:N0}").FontSize(9).Bold();

                    table.Cell().Background(bgColor).BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignCenter()
                        .Text(sale.PaymentStatus)
                        .FontSize(8)
                        .FontColor(isPaid ? SuccessColor : ErrorColor)
                        .Bold();
                }

                // Total row
                table.Cell().ColumnSpan(5).Background(BackgroundLight).Padding(5).AlignRight()
                    .Text("TOTAL:").FontSize(9).Bold();

                table.Cell().Background(BackgroundLight).Padding(5).AlignRight()
                    .Text($"KES {report.TotalSales:N0}").FontSize(10).Bold().FontColor(SuccessColor);

                table.Cell().Background(BackgroundLight);
            });
        });
    }

    private void ComposeExpensesTable(IContainer container, ClerkReportData report)
    {
        container.Border(1).BorderColor(BorderColor).Column(column =>
        {
            column.Item().Background(ErrorColor).Padding(10)
                .Text($"EXPENSES BREAKDOWN ({report.ExpenseItems.Count} items)")
                .FontSize(10)
                .Bold()
                .FontColor(Colors.White);

            column.Item().Background(Colors.White).Padding(10).Column(col =>
            {
                // Commission expenses (detailed lines)
                var commissionExpenses = report.ExpenseItems.Where(e => e.LineType == "Commission Expense").ToList();
                if (commissionExpenses.Any())
                {
                    col.Item().Text("SALE COMMISSIONS").FontSize(9).Bold().FontColor(WarningColor);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(50);   // Date
                            columns.RelativeColumn();     // Description
                            columns.ConstantColumn(80);   // Amount
                        });

                        foreach (var expense in commissionExpenses.OrderBy(e => e.ItemDate))
                        {
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                                .Text(expense.ItemDate.ToString("dd/MM/yy")).FontSize(8).FontColor(TextSecondary);

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                                .Text(expense.LineItem).FontSize(8);

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                                .Text($"KES {expense.Amount:N0}").FontSize(8);
                        }

                        // Subtotal
                        var commissionTotal = commissionExpenses.Sum(e => e.Amount);
                        table.Cell().ColumnSpan(2).Background(BackgroundLight).Padding(5).AlignRight()
                            .Text("Subtotal:").FontSize(8).Bold();
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight()
                            .Text($"KES {commissionTotal:N0}").FontSize(9).Bold().FontColor(WarningColor);
                    });
                    col.Item().PaddingBottom(10);
                }

                // Loaders fee expenses (detailed lines)
                var loadersFeeExpenses = report.ExpenseItems.Where(e => e.LineType == "Loaders Fee Expense").ToList();
                if (loadersFeeExpenses.Any())
                {
                    col.Item().Text("LOADERS FEES").FontSize(9).Bold().FontColor(PrimaryColor);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(50);   // Date
                            columns.RelativeColumn();     // Description
                            columns.ConstantColumn(80);   // Amount
                        });

                        foreach (var expense in loadersFeeExpenses.OrderBy(e => e.ItemDate))
                        {
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                                .Text(expense.ItemDate.ToString("dd/MM/yy")).FontSize(8).FontColor(TextSecondary);

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                                .Text(expense.LineItem).FontSize(8);

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                                .Text($"KES {expense.Amount:N0}").FontSize(8);
                        }

                        // Subtotal
                        var loadersFeeTotal = loadersFeeExpenses.Sum(e => e.Amount);
                        table.Cell().ColumnSpan(2).Background(BackgroundLight).Padding(5).AlignRight()
                            .Text("Subtotal:").FontSize(8).Bold();
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight()
                            .Text($"KES {loadersFeeTotal:N0}").FontSize(9).Bold().FontColor(PrimaryColor);
                    });
                    col.Item().PaddingBottom(10);
                }

                // Land rate expenses (detailed lines)
                var landRateExpenses = report.ExpenseItems.Where(e => e.LineType == "Land Rate Fee Expense").ToList();
                if (landRateExpenses.Any())
                {
                    col.Item().Text("LAND RATE FEES").FontSize(9).Bold().FontColor(SuccessColor);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(50);   // Date
                            columns.RelativeColumn();     // Description
                            columns.ConstantColumn(80);   // Amount
                        });

                        foreach (var expense in landRateExpenses.OrderBy(e => e.ItemDate))
                        {
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                                .Text(expense.ItemDate.ToString("dd/MM/yy")).FontSize(8).FontColor(TextSecondary);

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                                .Text(expense.LineItem).FontSize(8);

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                                .Text($"KES {expense.Amount:N0}").FontSize(8);
                        }

                        // Subtotal
                        var landRateTotal = landRateExpenses.Sum(e => e.Amount);
                        table.Cell().ColumnSpan(2).Background(BackgroundLight).Padding(5).AlignRight()
                            .Text("Subtotal:").FontSize(8).Bold();
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight()
                            .Text($"KES {landRateTotal:N0}").FontSize(9).Bold().FontColor(SuccessColor);
                    });
                    col.Item().PaddingBottom(10);
                }

                // User expenses (detailed lines)
                var userExpenses = report.ExpenseItems.Where(e => e.LineType == "User Expense").ToList();
                if (userExpenses.Any())
                {
                    col.Item().Text("OTHER EXPENSES").FontSize(9).Bold().FontColor(TextSecondary);
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(50);   // Date
                            columns.RelativeColumn();     // Description
                            columns.ConstantColumn(80);   // Amount
                        });

                        foreach (var expense in userExpenses.OrderBy(e => e.ItemDate))
                        {
                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                                .Text(expense.ItemDate.ToString("dd/MM/yy")).FontSize(8).FontColor(TextSecondary);

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                                .Text(expense.LineItem).FontSize(8);

                            table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                                .Text($"KES {expense.Amount:N0}").FontSize(8);
                        }

                        // Subtotal
                        var userExpensesTotal = userExpenses.Sum(e => e.Amount);
                        table.Cell().ColumnSpan(2).Background(BackgroundLight).Padding(5).AlignRight()
                            .Text("Subtotal:").FontSize(8).Bold();
                        table.Cell().Background(BackgroundLight).Padding(5).AlignRight()
                            .Text($"KES {userExpensesTotal:N0}").FontSize(9).Bold();
                    });
                    col.Item().PaddingBottom(10);
                }

                // Grand Total
                col.Item().PaddingTop(5).AlignRight()
                    .Text($"TOTAL EXPENSES: KES {report.TotalExpenses:N0}")
                    .FontSize(11)
                    .Bold()
                    .FontColor(ErrorColor);
            });
        });
    }

    private void ComposeFuelUsageTable(IContainer container, ClerkReportData report)
    {
        container.Border(1).BorderColor(BorderColor).Column(column =>
        {
            column.Item().Background(WarningColor).Padding(10)
                .Text($"FUEL USAGE ({report.FuelUsages.Count} records)")
                .FontSize(10)
                .Bold()
                .FontColor(Colors.White);

            column.Item().Background(Colors.White).Padding(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(50);   // Date
                    columns.RelativeColumn();     // Old Stock
                    columns.RelativeColumn();     // New Stock
                    columns.RelativeColumn();     // Machines
                    columns.RelativeColumn();     // W/Loaders
                    columns.RelativeColumn();     // Balance
                });

                table.Header(header =>
                {
                    header.Cell().Background(BackgroundLight).Padding(5).Text("DATE").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("OLD STOCK").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("NEW STOCK").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("MACHINES").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("W/LOADERS").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("BALANCE").FontSize(8).Bold();
                });

                foreach (var fuel in report.FuelUsages.OrderBy(f => f.UsageDate))
                {
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(fuel.UsageDate?.ToString("dd/MM/yy") ?? "").FontSize(9);

                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"{fuel.OldStock:N1}L").FontSize(9);

                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"{fuel.NewStock:N1}L").FontSize(9);

                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"{fuel.MachinesLoaded:N1}L").FontSize(9);

                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"{fuel.WheelLoadersLoaded:N1}L").FontSize(9);

                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"{fuel.Balance:N1}L").FontSize(9).Bold().FontColor(SuccessColor);
                }
            });
        });
    }

    private void ComposeBankingTable(IContainer container, ClerkReportData report)
    {
        container.Border(1).BorderColor(BorderColor).Column(column =>
        {
            column.Item().Background(PrimaryColor).Padding(10)
                .Text($"BANKING TRANSACTIONS ({report.Bankings.Count} records)")
                .FontSize(10)
                .Bold()
                .FontColor(Colors.White);

            column.Item().Background(Colors.White).Padding(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(50);   // Date
                    columns.RelativeColumn(2);    // Description
                    columns.RelativeColumn();     // Reference
                    columns.ConstantColumn(80);   // Amount
                });

                table.Header(header =>
                {
                    header.Cell().Background(BackgroundLight).Padding(5).Text("DATE").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).Text("DESCRIPTION").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).Text("REFERENCE").FontSize(8).Bold();
                    header.Cell().Background(BackgroundLight).Padding(5).AlignRight().Text("AMOUNT").FontSize(8).Bold();
                });

                foreach (var banking in report.Bankings.OrderBy(b => b.BankingDate))
                {
                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(banking.BankingDate?.ToString("dd/MM/yy") ?? "").FontSize(9);

                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(banking.Item).FontSize(9);

                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5)
                        .Text(banking.RefCode ?? "").FontSize(8).FontColor(TextSecondary);

                    table.Cell().BorderBottom(1).BorderColor(BorderColor).Padding(5).AlignRight()
                        .Text($"KES {banking.AmountBanked:N0}").FontSize(9).Bold();
                }

                // Total row
                table.Cell().ColumnSpan(3).Background(BackgroundLight).Padding(5).AlignRight()
                    .Text("TOTAL BANKED:").FontSize(9).Bold();

                table.Cell().Background(BackgroundLight).Padding(5).AlignRight()
                    .Text($"KES {report.Banked:N0}").FontSize(10).Bold().FontColor(PrimaryColor);
            });
        });
    }

    private void ComposeFooter(IContainer container)
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
                    text.Span(" • Quarry Management System").FontSize(8).FontColor(TextSecondary);
                });

                row.RelativeItem().AlignCenter().Text(text =>
                {
                    text.Span(DateTime.Now.ToString("MMMM dd, yyyy")).FontSize(8).FontColor(TextSecondary);
                });

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

    #endregion

    #region Excel Helper Methods

    /// <summary>
    /// Convert hex color string to XLColor
    /// </summary>
    private static XLColor HexToXLColor(string hex)
    {
        // Remove # if present
        hex = hex.TrimStart('#');

        // Parse RGB values
        var r = Convert.ToByte(hex.Substring(0, 2), 16);
        var g = Convert.ToByte(hex.Substring(2, 2), 16);
        var b = Convert.ToByte(hex.Substring(4, 2), 16);

        return XLColor.FromArgb(r, g, b);
    }

    #endregion

    #region Excel Composition Methods

    private void ComposeSummarySheet(IXLWorksheet sheet, ClerkReportData report, string? dailyNotes)
    {
        // Title
        sheet.Cell(1, 1).Value = "SALES REPORT";
        sheet.Cell(1, 1).Style.Font.FontSize = 18;
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontColor = HexToXLColor("#1976D2");

        sheet.Cell(2, 1).Value = report.ReportTitle;
        sheet.Cell(2, 1).Style.Font.FontSize = 12;
        sheet.Cell(2, 1).Style.Font.Italic = true;

        sheet.Cell(3, 1).Value = $"{report.QuarryName} • {report.ClerkName}";
        sheet.Cell(3, 1).Style.Font.FontSize = 10;

        int row = 5;

        // Summary Section
        sheet.Cell(row, 1).Value = "FINANCIAL SUMMARY";
        sheet.Range(row, 1, row, 2).Merge().Style.Fill.BackgroundColor = HexToXLColor("#1976D2");
        sheet.Range(row, 1, row, 2).Style.Font.FontColor = XLColor.White;
        sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
        row++;

        if (report.IsSingleDay)
        {
            sheet.Cell(row, 1).Value = "Opening Balance (B/F):";
            sheet.Cell(row, 2).Value = report.OpeningBalance;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
        }

        sheet.Cell(row, 1).Value = "Quantity:";
        sheet.Cell(row, 2).Value = report.TotalQuantity;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        row++;

        sheet.Cell(row, 1).Value = "Sales:";
        sheet.Cell(row, 2).Value = report.TotalSales;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 2).Style.Font.FontColor = HexToXLColor("#4CAF50");
        sheet.Cell(row, 2).Style.Font.Bold = true;
        row++;

        sheet.Cell(row, 1).Value = "  • Commissions:";
        sheet.Cell(row, 2).Value = report.Commission;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        row++;

        sheet.Cell(row, 1).Value = "  • Loaders Fee:";
        sheet.Cell(row, 2).Value = report.LoadersFee;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        row++;

        if (report.LandRateVisible)
        {
            sheet.Cell(row, 1).Value = "  • Land Rate:";
            sheet.Cell(row, 2).Value = report.LandRateFee;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            row++;
        }

        sheet.Cell(row, 1).Value = "Total Expenses:";
        sheet.Cell(row, 2).Value = report.TotalExpenses;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 2).Style.Font.FontColor = HexToXLColor("#F44336");
        sheet.Cell(row, 2).Style.Font.Bold = true;
        row++;

        sheet.Cell(row, 1).Value = "Earnings:";
        sheet.Cell(row, 2).Value = report.Earnings;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Style.Font.Bold = true;
        row++;

        if (report.UnpaidOrders)
        {
            sheet.Cell(row, 1).Value = "Unpaid Orders:";
            sheet.Cell(row, 2).Value = report.Unpaid;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 2).Style.Font.FontColor = HexToXLColor("#F44336");
            row++;
        }

        sheet.Cell(row, 1).Value = "Net Income:";
        sheet.Cell(row, 2).Value = report.NetEarnings;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Style.Font.Bold = true;
        row++;

        sheet.Cell(row, 1).Value = "Banked:";
        sheet.Cell(row, 2).Value = report.Banked;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        row++;

        sheet.Cell(row, 1).Value = "Closing Balance (C/H):";
        sheet.Cell(row, 2).Value = report.CashInHand;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = HexToXLColor("#E3F2FD");
        sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 2).Style.Font.FontColor = HexToXLColor("#1976D2");
        row++;

        // Daily Notes
        if (!string.IsNullOrWhiteSpace(dailyNotes))
        {
            row++;
            sheet.Cell(row, 1).Value = "Daily Notes:";
            sheet.Cell(row, 1).Style.Font.Bold = true;
            row++;
            sheet.Cell(row, 1).Value = dailyNotes;
            sheet.Cell(row, 1).Style.Font.Italic = true;
            sheet.Range(row, 1, row, 2).Merge();
        }

        // Auto-fit columns
        sheet.Columns().AdjustToContents();
    }

    private void ComposeSalesSheet(IXLWorksheet sheet, ClerkReportData report)
    {
        // Header
        sheet.Cell(1, 1).Value = "SALES BREAKDOWN";
        sheet.Range(1, 1, 1, 7).Merge().Style.Fill.BackgroundColor = HexToXLColor("#4CAF50");
        sheet.Range(1, 1, 1, 7).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, 7).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Column headers
        int row = 2;
        sheet.Cell(row, 1).Value = "Date";
        sheet.Cell(row, 2).Value = "Vehicle";
        sheet.Cell(row, 3).Value = "Product";
        sheet.Cell(row, 4).Value = "Quantity";
        sheet.Cell(row, 5).Value = "Price";
        sheet.Cell(row, 6).Value = "Amount";
        sheet.Cell(row, 7).Value = "Status";

        sheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = HexToXLColor("#F5F5F5");
        sheet.Range(row, 1, row, 7).Style.Font.Bold = true;
        sheet.Range(row, 1, row, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // Data
        row = 3;
        foreach (var sale in report.Sales.OrderBy(s => s.SaleDate))
        {
            sheet.Cell(row, 1).Value = sale.SaleDate?.ToString("dd/MM/yy") ?? "";
            sheet.Cell(row, 2).Value = sale.VehicleRegistration;
            sheet.Cell(row, 3).Value = sale.Product?.ProductName ?? "";
            sheet.Cell(row, 4).Value = sale.Quantity;
            sheet.Cell(row, 5).Value = sale.PricePerUnit;
            sheet.Cell(row, 6).Value = sale.GrossSaleAmount;
            sheet.Cell(row, 7).Value = sale.PaymentStatus;

            if (sale.PaymentStatus != "Paid")
            {
                sheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = HexToXLColor("#FFEBEE");
                sheet.Cell(row, 7).Style.Font.FontColor = HexToXLColor("#F44336");
            }

            row++;
        }

        // Total row
        sheet.Cell(row, 1).Value = "TOTAL:";
        sheet.Range(row, 1, row, 5).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 6).Value = report.TotalSales;
        sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = HexToXLColor("#E8F5E9");
        sheet.Range(row, 1, row, 7).Style.Font.Bold = true;
        sheet.Cell(row, 6).Style.Font.FontColor = HexToXLColor("#4CAF50");

        sheet.Columns().AdjustToContents();
    }

    private void ComposeExpensesSheet(IXLWorksheet sheet, ClerkReportData report)
    {
        // Header
        sheet.Cell(1, 1).Value = "EXPENSES BREAKDOWN";
        sheet.Range(1, 1, 1, 4).Merge().Style.Fill.BackgroundColor = HexToXLColor("#F44336");
        sheet.Range(1, 1, 1, 4).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, 4).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = 3;

        // Commission expenses (detailed lines)
        var commissionExpenses = report.ExpenseItems.Where(e => e.LineType == "Commission Expense").ToList();
        if (commissionExpenses.Any())
        {
            sheet.Cell(row, 1).Value = "SALE COMMISSIONS";
            sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#FFF3E0");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 3).Style.Font.FontColor = HexToXLColor("#FF9800");
            row++;

            sheet.Cell(row, 1).Value = "Date";
            sheet.Cell(row, 2).Value = "Description";
            sheet.Cell(row, 3).Value = "Amount";
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#F5F5F5");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            row++;

            foreach (var expense in commissionExpenses.OrderBy(e => e.ItemDate))
            {
                sheet.Cell(row, 1).Value = expense.ItemDate.ToString("dd/MM/yy");
                sheet.Cell(row, 2).Value = expense.LineItem;
                sheet.Cell(row, 3).Value = expense.Amount;
                sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            // Subtotal
            sheet.Cell(row, 1).Value = "Subtotal:";
            sheet.Range(row, 1, row, 2).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Cell(row, 3).Value = commissionExpenses.Sum(e => e.Amount);
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#FFF3E0");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            sheet.Cell(row, 3).Style.Font.FontColor = HexToXLColor("#FF9800");
            row++;
            row++; // Empty row
        }

        // Loaders fee expenses (detailed lines)
        var loadersFeeExpenses = report.ExpenseItems.Where(e => e.LineType == "Loaders Fee Expense").ToList();
        if (loadersFeeExpenses.Any())
        {
            sheet.Cell(row, 1).Value = "LOADERS FEES";
            sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#E3F2FD");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 3).Style.Font.FontColor = HexToXLColor("#1976D2");
            row++;

            sheet.Cell(row, 1).Value = "Date";
            sheet.Cell(row, 2).Value = "Description";
            sheet.Cell(row, 3).Value = "Amount";
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#F5F5F5");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            row++;

            foreach (var expense in loadersFeeExpenses.OrderBy(e => e.ItemDate))
            {
                sheet.Cell(row, 1).Value = expense.ItemDate.ToString("dd/MM/yy");
                sheet.Cell(row, 2).Value = expense.LineItem;
                sheet.Cell(row, 3).Value = expense.Amount;
                sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            // Subtotal
            sheet.Cell(row, 1).Value = "Subtotal:";
            sheet.Range(row, 1, row, 2).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Cell(row, 3).Value = loadersFeeExpenses.Sum(e => e.Amount);
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#E3F2FD");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            sheet.Cell(row, 3).Style.Font.FontColor = HexToXLColor("#1976D2");
            row++;
            row++; // Empty row
        }

        // Land rate expenses (detailed lines)
        var landRateExpenses = report.ExpenseItems.Where(e => e.LineType == "Land Rate Fee Expense").ToList();
        if (landRateExpenses.Any())
        {
            sheet.Cell(row, 1).Value = "LAND RATE FEES";
            sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#E8F5E9");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            sheet.Range(row, 1, row, 3).Style.Font.FontColor = HexToXLColor("#4CAF50");
            row++;

            sheet.Cell(row, 1).Value = "Date";
            sheet.Cell(row, 2).Value = "Description";
            sheet.Cell(row, 3).Value = "Amount";
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#F5F5F5");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            row++;

            foreach (var expense in landRateExpenses.OrderBy(e => e.ItemDate))
            {
                sheet.Cell(row, 1).Value = expense.ItemDate.ToString("dd/MM/yy");
                sheet.Cell(row, 2).Value = expense.LineItem;
                sheet.Cell(row, 3).Value = expense.Amount;
                sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            // Subtotal
            sheet.Cell(row, 1).Value = "Subtotal:";
            sheet.Range(row, 1, row, 2).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Cell(row, 3).Value = landRateExpenses.Sum(e => e.Amount);
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#E8F5E9");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            sheet.Cell(row, 3).Style.Font.FontColor = HexToXLColor("#4CAF50");
            row++;
            row++; // Empty row
        }

        // User expenses (detailed lines)
        var userExpenses = report.ExpenseItems.Where(e => e.LineType == "User Expense").ToList();
        if (userExpenses.Any())
        {
            sheet.Cell(row, 1).Value = "OTHER EXPENSES";
            sheet.Range(row, 1, row, 3).Merge().Style.Fill.BackgroundColor = HexToXLColor("#F5F5F5");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            row++;

            sheet.Cell(row, 1).Value = "Date";
            sheet.Cell(row, 2).Value = "Description";
            sheet.Cell(row, 3).Value = "Amount";
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#F5F5F5");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            row++;

            foreach (var expense in userExpenses.OrderBy(e => e.ItemDate))
            {
                sheet.Cell(row, 1).Value = expense.ItemDate.ToString("dd/MM/yy");
                sheet.Cell(row, 2).Value = expense.LineItem;
                sheet.Cell(row, 3).Value = expense.Amount;
                sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
                row++;
            }

            // Subtotal
            sheet.Cell(row, 1).Value = "Subtotal:";
            sheet.Range(row, 1, row, 2).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Cell(row, 3).Value = userExpenses.Sum(e => e.Amount);
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            sheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = HexToXLColor("#F5F5F5");
            sheet.Range(row, 1, row, 3).Style.Font.Bold = true;
            row++;
            row++; // Empty row
        }

        row++;
        sheet.Cell(row, 1).Value = "TOTAL EXPENSES:";
        sheet.Cell(row, 2).Value = report.TotalExpenses;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = HexToXLColor("#FFEBEE");
        sheet.Range(row, 1, row, 2).Style.Font.Bold = true;
        sheet.Cell(row, 2).Style.Font.FontColor = HexToXLColor("#F44336");

        sheet.Columns().AdjustToContents();
    }

    private void ComposeFuelUsageSheet(IXLWorksheet sheet, ClerkReportData report)
    {
        // Header
        sheet.Cell(1, 1).Value = "FUEL USAGE";
        sheet.Range(1, 1, 1, 6).Merge().Style.Fill.BackgroundColor = HexToXLColor("#FF9800");
        sheet.Range(1, 1, 1, 6).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, 6).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Column headers
        int row = 2;
        sheet.Cell(row, 1).Value = "Date";
        sheet.Cell(row, 2).Value = "Old Stock (L)";
        sheet.Cell(row, 3).Value = "New Stock (L)";
        sheet.Cell(row, 4).Value = "Machines (L)";
        sheet.Cell(row, 5).Value = "W/Loaders (L)";
        sheet.Cell(row, 6).Value = "Balance (L)";

        sheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = HexToXLColor("#FFF3E0");
        sheet.Range(row, 1, row, 6).Style.Font.Bold = true;

        // Data
        row = 3;
        foreach (var fuel in report.FuelUsages.OrderBy(f => f.UsageDate))
        {
            sheet.Cell(row, 1).Value = fuel.UsageDate?.ToString("dd/MM/yy") ?? "";
            sheet.Cell(row, 2).Value = fuel.OldStock;
            sheet.Cell(row, 3).Value = fuel.NewStock;
            sheet.Cell(row, 4).Value = fuel.MachinesLoaded;
            sheet.Cell(row, 5).Value = fuel.WheelLoadersLoaded;
            sheet.Cell(row, 6).Value = fuel.Balance;

            sheet.Range(row, 2, row, 6).Style.NumberFormat.Format = "#,##0.0";
            sheet.Cell(row, 6).Style.Font.Bold = true;
            sheet.Cell(row, 6).Style.Font.FontColor = HexToXLColor("#4CAF50");

            row++;
        }

        sheet.Columns().AdjustToContents();
    }

    private void ComposeBankingSheet(IXLWorksheet sheet, ClerkReportData report)
    {
        // Header
        sheet.Cell(1, 1).Value = "BANKING TRANSACTIONS";
        sheet.Range(1, 1, 1, 4).Merge().Style.Fill.BackgroundColor = HexToXLColor("#1976D2");
        sheet.Range(1, 1, 1, 4).Style.Font.FontColor = XLColor.White;
        sheet.Range(1, 1, 1, 4).Style.Font.Bold = true;
        sheet.Range(1, 1, 1, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Column headers
        int row = 2;
        sheet.Cell(row, 1).Value = "Date";
        sheet.Cell(row, 2).Value = "Description";
        sheet.Cell(row, 3).Value = "Reference";
        sheet.Cell(row, 4).Value = "Amount";

        sheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = HexToXLColor("#E3F2FD");
        sheet.Range(row, 1, row, 4).Style.Font.Bold = true;

        // Data
        row = 3;
        foreach (var banking in report.Bankings.OrderBy(b => b.BankingDate))
        {
            sheet.Cell(row, 1).Value = banking.BankingDate?.ToString("dd/MM/yy") ?? "";
            sheet.Cell(row, 2).Value = banking.Item;
            sheet.Cell(row, 3).Value = banking.RefCode ?? "";
            sheet.Cell(row, 4).Value = banking.AmountBanked;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0";

            row++;
        }

        // Total row
        sheet.Cell(row, 1).Value = "TOTAL BANKED:";
        sheet.Range(row, 1, row, 3).Merge().Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sheet.Cell(row, 4).Value = report.Banked;
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
        sheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = HexToXLColor("#E3F2FD");
        sheet.Range(row, 1, row, 4).Style.Font.Bold = true;
        sheet.Cell(row, 4).Style.Font.FontColor = HexToXLColor("#1976D2");

        sheet.Columns().AdjustToContents();
    }

    #endregion
}
