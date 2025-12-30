using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Enums;
using QDeskPro.Features.Accounting.Models;

namespace QDeskPro.Features.Accounting.Services;

/// <summary>
/// Service for generating financial reports from accounting data.
/// </summary>
public interface IFinancialReportService
{
    Task<TrialBalanceReport> GenerateTrialBalanceAsync(string quarryId, DateTime asOfDate);
    Task<ProfitLossReport> GenerateProfitLossAsync(string quarryId, DateTime from, DateTime to);
    Task<BalanceSheetReport> GenerateBalanceSheetAsync(string quarryId, DateTime asOfDate);
    Task<CashFlowReport> GenerateCashFlowAsync(string quarryId, DateTime from, DateTime to);
    Task<ARAgingReport> GenerateARAgingAsync(string quarryId, DateTime asOfDate);
    Task<APSummaryReport> GenerateAPSummaryAsync(string quarryId, DateTime asOfDate);
    Task<GeneralLedgerReport> GenerateGeneralLedgerAsync(string quarryId, string accountId, DateTime from, DateTime to);
}

/// <summary>
/// Implementation of financial report generation service.
/// </summary>
public class FinancialReportService : IFinancialReportService
{
    private readonly AppDbContext _context;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<FinancialReportService> _logger;

    public FinancialReportService(
        AppDbContext context,
        IAccountingService accountingService,
        ILogger<FinancialReportService> logger)
    {
        _context = context;
        _accountingService = accountingService;
        _logger = logger;
    }

    public async Task<TrialBalanceReport> GenerateTrialBalanceAsync(string quarryId, DateTime asOfDate)
    {
        var quarry = await _context.Quarries.FindAsync(quarryId);
        var accounts = await _accountingService.GetChartOfAccountsAsync(quarryId);
        var balances = await _accountingService.GetAllAccountBalancesAsync(quarryId, asOfDate);

        var report = new TrialBalanceReport
        {
            QuarryId = quarryId,
            QuarryName = quarry?.QuarryName ?? "Unknown Quarry",
            AsOfDate = asOfDate,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var account in accounts)
        {
            var balance = balances.GetValueOrDefault(account.Id, 0);
            if (Math.Abs(balance) < 0.01) continue; // Skip zero balances

            var line = new TrialBalanceLine
            {
                AccountCode = account.AccountCode,
                AccountName = account.AccountName,
                Category = account.Category
            };

            // Place balance in appropriate column based on normal balance
            if (account.IsDebitNormal)
            {
                if (balance >= 0)
                    line.DebitBalance = balance;
                else
                    line.CreditBalance = Math.Abs(balance);
            }
            else
            {
                if (balance >= 0)
                    line.CreditBalance = balance;
                else
                    line.DebitBalance = Math.Abs(balance);
            }

            report.Lines.Add(line);
        }

        // Sort by account code
        report.Lines = report.Lines.OrderBy(l => l.AccountCode).ToList();

        _logger.LogInformation("Generated Trial Balance for quarry {QuarryId} as of {AsOfDate}",
            quarryId, asOfDate);

        return report;
    }

    public async Task<ProfitLossReport> GenerateProfitLossAsync(string quarryId, DateTime from, DateTime to)
    {
        var quarry = await _context.Quarries.FindAsync(quarryId);

        var report = new ProfitLossReport
        {
            QuarryId = quarryId,
            QuarryName = quarry?.QuarryName ?? "Unknown Quarry",
            PeriodStart = from,
            PeriodEnd = to,
            GeneratedAt = DateTime.UtcNow
        };

        // Get all journal entry lines for the period
        var journalLines = await _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Include(l => l.LedgerAccount)
            .Where(l => l.JournalEntry.QId == quarryId)
            .Where(l => l.JournalEntry.IsActive && l.JournalEntry.IsPosted)
            .Where(l => l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to)
            .ToListAsync();

        // Group by account and calculate amounts
        var accountTotals = journalLines
            .GroupBy(l => l.LedgerAccount)
            .Select(g => new
            {
                Account = g.Key,
                NetAmount = g.Key.IsDebitNormal
                    ? g.Sum(l => l.DebitAmount) - g.Sum(l => l.CreditAmount)
                    : g.Sum(l => l.CreditAmount) - g.Sum(l => l.DebitAmount)
            })
            .ToList();

        // Revenue items (Category = Revenue)
        var revenueItems = accountTotals
            .Where(a => a.Account.Category == AccountCategory.Revenue && Math.Abs(a.NetAmount) > 0.01)
            .Select(a => new ProfitLossLineItem
            {
                AccountCode = a.Account.AccountCode,
                Description = a.Account.AccountName,
                Amount = Math.Abs(a.NetAmount) // Revenue should be positive
            })
            .OrderBy(i => i.AccountCode)
            .ToList();

        report.RevenueItems = revenueItems;

        // Cost of Sales items (Category = CostOfSales)
        var costOfSalesItems = accountTotals
            .Where(a => a.Account.Category == AccountCategory.CostOfSales && Math.Abs(a.NetAmount) > 0.01)
            .Select(a => new ProfitLossLineItem
            {
                AccountCode = a.Account.AccountCode,
                Description = a.Account.AccountName,
                Amount = Math.Abs(a.NetAmount)
            })
            .OrderBy(i => i.AccountCode)
            .ToList();

        report.CostOfSalesItems = costOfSalesItems;

        // Operating Expenses (Category = Expenses)
        var operatingExpenses = accountTotals
            .Where(a => a.Account.Category == AccountCategory.Expenses && Math.Abs(a.NetAmount) > 0.01)
            .Select(a => new ProfitLossLineItem
            {
                AccountCode = a.Account.AccountCode,
                Description = a.Account.AccountName,
                Amount = Math.Abs(a.NetAmount)
            })
            .OrderBy(i => i.AccountCode)
            .ToList();

        report.OperatingExpenses = operatingExpenses;

        // Calculate percentages
        if (report.TotalRevenue > 0)
        {
            foreach (var item in report.RevenueItems)
                item.Percentage = (item.Amount / report.TotalRevenue) * 100;
            foreach (var item in report.CostOfSalesItems)
                item.Percentage = (item.Amount / report.TotalRevenue) * 100;
            foreach (var item in report.OperatingExpenses)
                item.Percentage = (item.Amount / report.TotalRevenue) * 100;
        }

        _logger.LogInformation("Generated P&L for quarry {QuarryId} from {From} to {To}",
            quarryId, from, to);

        return report;
    }

    public async Task<BalanceSheetReport> GenerateBalanceSheetAsync(string quarryId, DateTime asOfDate)
    {
        var quarry = await _context.Quarries.FindAsync(quarryId);
        var accounts = await _accountingService.GetChartOfAccountsAsync(quarryId);
        var balances = await _accountingService.GetAllAccountBalancesAsync(quarryId, asOfDate);

        var report = new BalanceSheetReport
        {
            QuarryId = quarryId,
            QuarryName = quarry?.QuarryName ?? "Unknown Quarry",
            AsOfDate = asOfDate,
            GeneratedAt = DateTime.UtcNow
        };

        foreach (var account in accounts)
        {
            var balance = balances.GetValueOrDefault(account.Id, 0);
            if (Math.Abs(balance) < 0.01) continue;

            var line = new BalanceSheetLineItem
            {
                AccountCode = account.AccountCode,
                Description = account.AccountName,
                Amount = Math.Abs(balance)
            };

            switch (account.Category)
            {
                case AccountCategory.Assets:
                    // Current assets: Cash, Bank, Receivables (codes 1000-1499)
                    if (int.Parse(account.AccountCode) < 1500)
                        report.CurrentAssets.Add(line);
                    else
                        report.NonCurrentAssets.Add(line);
                    break;

                case AccountCategory.Liabilities:
                    // Current liabilities: Customer Deposits, Accrued (codes 2000-2299)
                    if (int.Parse(account.AccountCode) < 2300)
                        report.CurrentLiabilities.Add(line);
                    else
                        report.NonCurrentLiabilities.Add(line);
                    break;

                case AccountCategory.Equity:
                    report.EquityItems.Add(line);
                    break;
            }
        }

        // Calculate current period profit/loss
        var periodStart = new DateTime(asOfDate.Year, 1, 1); // Start of fiscal year
        var pnl = await GenerateProfitLossAsync(quarryId, periodStart, asOfDate);
        report.CurrentPeriodProfitLoss = pnl.NetProfit;

        // Sort items by account code
        report.CurrentAssets = report.CurrentAssets.OrderBy(i => i.AccountCode).ToList();
        report.NonCurrentAssets = report.NonCurrentAssets.OrderBy(i => i.AccountCode).ToList();
        report.CurrentLiabilities = report.CurrentLiabilities.OrderBy(i => i.AccountCode).ToList();
        report.NonCurrentLiabilities = report.NonCurrentLiabilities.OrderBy(i => i.AccountCode).ToList();
        report.EquityItems = report.EquityItems.OrderBy(i => i.AccountCode).ToList();

        _logger.LogInformation("Generated Balance Sheet for quarry {QuarryId} as of {AsOfDate}",
            quarryId, asOfDate);

        return report;
    }

    public async Task<CashFlowReport> GenerateCashFlowAsync(string quarryId, DateTime from, DateTime to)
    {
        var quarry = await _context.Quarries.FindAsync(quarryId);

        var report = new CashFlowReport
        {
            QuarryId = quarryId,
            QuarryName = quarry?.QuarryName ?? "Unknown Quarry",
            PeriodStart = from,
            PeriodEnd = to,
            GeneratedAt = DateTime.UtcNow
        };

        // Get opening cash balance
        var cashAccount = await _accountingService.GetAccountByCodeAsync(quarryId, "1000");
        if (cashAccount != null)
        {
            report.OpeningCashBalance = await _accountingService.GetAccountBalanceAsync(
                cashAccount.Id, from.AddDays(-1));
        }

        // Calculate cash inflows from sales (paid sales)
        var paidSales = await _context.Sales
            .Where(s => s.QId == quarryId && s.IsActive)
            .Where(s => s.SaleDate >= from && s.SaleDate <= to)
            .Where(s => s.PaymentStatus == "Paid")
            .SumAsync(s => s.Quantity * s.PricePerUnit);

        report.CashFromDirectSales = paidSales;
        report.OperatingInflows.Add(new CashFlowLineItem
        {
            Description = "Cash received from customers (paid sales)",
            Amount = paidSales
        });

        // Cash from prepayments
        var prepaymentReceipts = await _context.Prepayments
            .Where(p => p.QId == quarryId && p.IsActive)
            .Where(p => p.PrepaymentDate >= from && p.PrepaymentDate <= to)
            .SumAsync(p => p.TotalAmountPaid);

        if (prepaymentReceipts > 0)
        {
            report.CashFromPrepayments = prepaymentReceipts;
            report.OperatingInflows.Add(new CashFlowLineItem
            {
                Description = "Cash received from customer prepayments",
                Amount = prepaymentReceipts
            });
        }

        // Cash from collections (sales paid after the original sale date)
        var collections = await _context.Sales
            .Where(s => s.QId == quarryId && s.IsActive)
            .Where(s => s.PaymentReceivedDate >= from && s.PaymentReceivedDate <= to)
            .Where(s => s.PaymentStatus == "Paid" && s.PaymentReceivedDate != s.SaleDate)
            .SumAsync(s => s.Quantity * s.PricePerUnit);

        if (collections > 0)
        {
            report.CashFromCollections = collections;
            report.OperatingInflows.Add(new CashFlowLineItem
            {
                Description = "Cash received from collections (past sales)",
                Amount = collections
            });
        }

        // Calculate cash outflows from expenses
        var totalExpenses = await _context.Expenses
            .Where(e => e.QId == quarryId && e.IsActive)
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .SumAsync(e => e.Amount);

        if (totalExpenses > 0)
        {
            report.OperatingOutflows.Add(new CashFlowLineItem
            {
                Description = "Cash paid for operating expenses",
                Amount = totalExpenses
            });
        }

        // Get expense breakdown by category for detailed view
        var expensesByCategory = await _context.Expenses
            .Where(e => e.QId == quarryId && e.IsActive)
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .GroupBy(e => e.Category ?? "Miscellaneous")
            .Select(g => new { Category = g.Key, Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToListAsync();

        foreach (var expense in expensesByCategory)
        {
            if (expense.Amount > 0)
            {
                report.OperatingOutflows.Add(new CashFlowLineItem
                {
                    Description = $"  - {expense.Category}",
                    Amount = expense.Amount,
                    Notes = "Expense category breakdown"
                });
            }
        }

        // Cash deposited to bank
        var bankedAmount = await _context.Bankings
            .Where(b => b.QId == quarryId && b.IsActive)
            .Where(b => b.BankingDate >= from && b.BankingDate <= to)
            .SumAsync(b => b.AmountBanked);

        report.CashBanked = bankedAmount;

        _logger.LogInformation("Generated Cash Flow for quarry {QuarryId} from {From} to {To}",
            quarryId, from, to);

        return report;
    }

    public async Task<ARAgingReport> GenerateARAgingAsync(string quarryId, DateTime asOfDate)
    {
        var quarry = await _context.Quarries.FindAsync(quarryId);

        var report = new ARAgingReport
        {
            QuarryId = quarryId,
            QuarryName = quarry?.QuarryName ?? "Unknown Quarry",
            AsOfDate = asOfDate,
            GeneratedAt = DateTime.UtcNow
        };

        // Get all unpaid sales
        var unpaidSales = await _context.Sales
            .Include(s => s.Product)
            .Where(s => s.QId == quarryId && s.IsActive)
            .Where(s => s.PaymentStatus == "NotPaid")
            .Where(s => s.SaleDate <= asOfDate)
            .OrderBy(s => s.VehicleRegistration)
            .ThenBy(s => s.SaleDate)
            .ToListAsync();

        // Group by customer (Vehicle Registration)
        var customerGroups = unpaidSales
            .GroupBy(s => s.VehicleRegistration)
            .ToList();

        foreach (var group in customerGroups)
        {
            var customer = new ARAgingCustomer
            {
                CustomerId = group.Key,
                VehicleRegistration = group.Key,
                ClientName = group.First().ClientName,
                ClientPhone = group.First().ClientPhone,
                InvoiceCount = group.Count(),
                OldestInvoiceDate = group.Min(s => s.SaleDate)
            };

            if (customer.OldestInvoiceDate.HasValue)
            {
                customer.DaysSinceOldest = (asOfDate - customer.OldestInvoiceDate.Value).Days;
            }

            foreach (var sale in group)
            {
                var amount = sale.Quantity * sale.PricePerUnit;
                var daysOld = sale.SaleDate.HasValue
                    ? (asOfDate - sale.SaleDate.Value).Days
                    : 0;

                // Assign to aging bucket
                if (daysOld == 0)
                    customer.Current += amount;
                else if (daysOld <= 30)
                    customer.Days1To30 += amount;
                else if (daysOld <= 60)
                    customer.Days31To60 += amount;
                else if (daysOld <= 90)
                    customer.Days61To90 += amount;
                else
                    customer.Over90Days += amount;

                // Add invoice detail
                customer.Invoices.Add(new ARAgingInvoice
                {
                    SaleId = sale.Id,
                    SaleDate = sale.SaleDate ?? DateTime.Today,
                    DaysOutstanding = daysOld,
                    ProductName = sale.Product?.ProductName ?? "Unknown",
                    Quantity = sale.Quantity,
                    Amount = amount,
                    ClerkName = sale.ClerkName
                });
            }

            report.Customers.Add(customer);
        }

        // Sort by total outstanding descending
        report.Customers = report.Customers
            .OrderByDescending(c => c.TotalOutstanding)
            .ToList();

        _logger.LogInformation("Generated AR Aging for quarry {QuarryId} as of {AsOfDate}. Total outstanding: {Total:C}",
            quarryId, asOfDate, report.TotalOutstanding);

        return report;
    }

    public async Task<APSummaryReport> GenerateAPSummaryAsync(string quarryId, DateTime asOfDate)
    {
        var quarry = await _context.Quarries.FindAsync(quarryId);

        var report = new APSummaryReport
        {
            QuarryId = quarryId,
            QuarryName = quarry?.QuarryName ?? "Unknown Quarry",
            AsOfDate = asOfDate,
            GeneratedAt = DateTime.UtcNow
        };

        // Calculate current month's broker commissions payable
        var periodStart = new DateTime(asOfDate.Year, asOfDate.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        // Get sales with commission for current period
        var salesWithCommission = await _context.Sales
            .Include(s => s.Broker)
            .Include(s => s.Product)
            .Where(s => s.QId == quarryId && s.IsActive)
            .Where(s => s.BrokerId != null && s.CommissionPerUnit > 0)
            .Where(s => s.SaleDate >= periodStart && s.SaleDate <= periodEnd)
            .ToListAsync();

        // Group by broker
        var brokerGroups = salesWithCommission
            .GroupBy(s => s.Broker)
            .ToList();

        foreach (var group in brokerGroups)
        {
            if (group.Key == null) continue;

            var brokerPayable = new APBrokerPayable
            {
                BrokerId = group.Key.Id,
                BrokerName = group.Key.BrokerName,
                Phone = group.Key.Phone,
                Period = periodStart.ToString("MMMM yyyy"),
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                SalesCount = group.Count(),
                TotalQuantity = group.Sum(s => s.Quantity),
                AmountDue = group.Sum(s => s.Quantity * s.CommissionPerUnit)
            };

            foreach (var sale in group)
            {
                brokerPayable.Sales.Add(new APCommissionSale
                {
                    SaleId = sale.Id,
                    SaleDate = sale.SaleDate ?? DateTime.Today,
                    VehicleRegistration = sale.VehicleRegistration,
                    ProductName = sale.Product?.ProductName ?? "Unknown",
                    Quantity = sale.Quantity,
                    CommissionPerUnit = sale.CommissionPerUnit,
                    CommissionAmount = sale.Quantity * sale.CommissionPerUnit
                });
            }

            report.BrokerPayables.Add(brokerPayable);
        }

        // Sort by amount due descending
        report.BrokerPayables = report.BrokerPayables
            .OrderByDescending(b => b.AmountDue)
            .ToList();

        // Calculate accrued fees (Loaders, Land Rate) for current period
        var periodSales = await _context.Sales
            .Include(s => s.Product)
            .Where(s => s.QId == quarryId && s.IsActive)
            .Where(s => s.SaleDate >= periodStart && s.SaleDate <= periodEnd)
            .ToListAsync();

        if (quarry != null && quarry.LoadersFee > 0)
        {
            var loadersFeeTotal = periodSales.Sum(s => s.Quantity) * quarry.LoadersFee.Value;
            report.AccruedFees.Add(new APAccruedFee
            {
                FeeType = "Loaders Fees",
                Period = periodStart.ToString("MMMM yyyy"),
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                SalesCount = periodSales.Count,
                TotalQuantity = periodSales.Sum(s => s.Quantity),
                FeePerUnit = quarry.LoadersFee.Value,
                AmountDue = loadersFeeTotal
            });
        }

        if (quarry != null && quarry.LandRateFee > 0)
        {
            var landRateFeeTotal = periodSales.Sum(s =>
            {
                var isReject = s.Product?.ProductName.ToLower().Contains("reject") == true;
                var feeRate = isReject ? (quarry.RejectsFee ?? 0) : quarry.LandRateFee.Value;
                return s.Quantity * feeRate;
            });

            report.AccruedFees.Add(new APAccruedFee
            {
                FeeType = "Land Rate Fees",
                Period = periodStart.ToString("MMMM yyyy"),
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                SalesCount = periodSales.Count,
                TotalQuantity = periodSales.Sum(s => s.Quantity),
                FeePerUnit = quarry.LandRateFee.Value,
                AmountDue = landRateFeeTotal
            });
        }

        _logger.LogInformation("Generated AP Summary for quarry {QuarryId} as of {AsOfDate}. Total payable: {Total:C}",
            quarryId, asOfDate, report.TotalAccountsPayable);

        return report;
    }

    public async Task<GeneralLedgerReport> GenerateGeneralLedgerAsync(string quarryId, string accountId, DateTime from, DateTime to)
    {
        var quarry = await _context.Quarries.FindAsync(quarryId);
        var account = await _accountingService.GetAccountByIdAsync(accountId);

        if (account == null)
            throw new InvalidOperationException($"Account {accountId} not found");

        var report = new GeneralLedgerReport
        {
            QuarryId = quarryId,
            QuarryName = quarry?.QuarryName ?? "Unknown Quarry",
            AccountId = accountId,
            AccountCode = account.AccountCode,
            AccountName = account.AccountName,
            Category = account.Category,
            PeriodStart = from,
            PeriodEnd = to,
            GeneratedAt = DateTime.UtcNow
        };

        // Get opening balance (balance before period start)
        report.OpeningBalance = await _accountingService.GetAccountBalanceAsync(accountId, from.AddDays(-1));

        // Get all journal entry lines for this account in the period
        var lines = await _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.LedgerAccountId == accountId)
            .Where(l => l.JournalEntry.QId == quarryId)
            .Where(l => l.JournalEntry.IsActive)
            .Where(l => l.JournalEntry.EntryDate >= from && l.JournalEntry.EntryDate <= to)
            .OrderBy(l => l.JournalEntry.EntryDate)
            .ThenBy(l => l.JournalEntry.DateCreated)
            .ToListAsync();

        double runningBalance = report.OpeningBalance;

        foreach (var line in lines)
        {
            // Calculate new running balance based on normal balance type
            if (account.IsDebitNormal)
                runningBalance += line.DebitAmount - line.CreditAmount;
            else
                runningBalance += line.CreditAmount - line.DebitAmount;

            report.Entries.Add(new GeneralLedgerEntry
            {
                EntryDate = line.JournalEntry.EntryDate,
                Reference = line.JournalEntry.Reference,
                Description = line.JournalEntry.Description,
                SourceType = line.JournalEntry.SourceEntityType,
                SourceId = line.JournalEntry.SourceEntityId,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                RunningBalance = runningBalance,
                IsPosted = line.JournalEntry.IsPosted,
                CreatedBy = line.JournalEntry.CreatedBy
            });
        }

        _logger.LogInformation("Generated General Ledger for account {AccountCode} in quarry {QuarryId}",
            account.AccountCode, quarryId);

        return report;
    }
}
