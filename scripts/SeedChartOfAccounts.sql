-- =============================================
-- QDeskPro Chart of Accounts Seeding Script
-- Generated: 2024-12-30
-- Version: 1.2 (Added USE database)
-- =============================================
-- Run this script AFTER AddAccountingModule.sql
-- Seeds default Chart of Accounts for ALL active quarries
-- =============================================

-- =============================================
-- IMPORTANT: Set the correct database name below!
-- =============================================
USE [QDeskPro];  -- <-- Change this if your database has a different name

PRINT 'Using database: ' + DB_NAME();
PRINT '';

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

-- =============================================
-- CLEANUP: Deallocate any existing cursor from failed previous runs
-- =============================================
IF CURSOR_STATUS('global', 'QuarryCursor') >= -1
BEGIN
    IF CURSOR_STATUS('global', 'QuarryCursor') >= 0
        CLOSE QuarryCursor;
    DEALLOCATE QuarryCursor;
    PRINT 'Cleaned up existing QuarryCursor from previous failed run';
END

-- AccountCategory enum values:
-- Assets=1, Liabilities=2, Equity=3, Revenue=4, CostOfSales=5, Expenses=6

-- AccountType enum values:
-- Cash=100, Bank=101, AccountsReceivable=102, PrepaidExpenses=103, FixedAssets=104
-- CustomerDeposits=200, AccountsPayable=201, AccruedExpenses=202, LoansPayable=203
-- OwnersEquity=300, RetainedEarnings=301
-- SalesRevenue=400, OtherIncome=401
-- CommissionExpense=500, LoadersFees=501, LandRateFees=502
-- FuelExpense=600, TransportationHire=601, MaintenanceRepairs=602, ConsumablesUtilities=603
-- AdministrativeExpenses=604, MarketingExpenses=605, WagesSalaries=606, BankCharges=607
-- CessRoadFees=608, MiscellaneousExpenses=609

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @QuarryId NVARCHAR(450);
    DECLARE @QuarryName NVARCHAR(200);
    DECLARE @Now DATETIME2 = GETUTCDATE();
    DECLARE @QuarryCount INT = 0;
    DECLARE @SkippedCount INT = 0;

    -- Cursor to iterate through all active quarries
    DECLARE QuarryCursor CURSOR FOR
        SELECT Id, QuarryName FROM Quarries WHERE IsActive = 1;

    OPEN QuarryCursor;
    FETCH NEXT FROM QuarryCursor INTO @QuarryId, @QuarryName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Check if accounts already exist for this quarry
        IF NOT EXISTS (SELECT 1 FROM LedgerAccounts WHERE QId = @QuarryId)
        BEGIN
            PRINT 'Seeding Chart of Accounts for: ' + @QuarryName;

            -- ===== ASSETS (1000-1999) =====
            INSERT INTO LedgerAccounts (Id, QId, AccountCode, AccountName, Category, [Type], Description, IsDebitNormal, IsSystemAccount, DisplayOrder, IsActive, DateCreated, CreatedBy)
            VALUES
            (NEWID(), @QuarryId, '1000', 'Cash on Hand', 1, 100, 'Physical cash held by clerks', 1, 1, 1, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '1010', 'Bank Account', 1, 101, 'Deposited funds in bank accounts', 1, 1, 2, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '1100', 'Accounts Receivable', 1, 102, 'Unpaid customer sales - amounts owed to the quarry', 1, 1, 3, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '1200', 'Prepaid Expenses', 1, 103, 'Advance payments for future services', 1, 1, 4, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '1500', 'Fixed Assets', 1, 104, 'Equipment, vehicles, and machinery', 1, 1, 5, 1, @Now, 'System');

            -- ===== LIABILITIES (2000-2999) =====
            INSERT INTO LedgerAccounts (Id, QId, AccountCode, AccountName, Category, [Type], Description, IsDebitNormal, IsSystemAccount, DisplayOrder, IsActive, DateCreated, CreatedBy)
            VALUES
            (NEWID(), @QuarryId, '2000', 'Customer Deposits', 2, 200, 'Prepayments received from customers - advance payments', 0, 1, 6, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '2100', 'Accrued Expenses', 2, 202, 'Expenses incurred but not yet paid (broker commissions, fees)', 0, 1, 7, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '2200', 'Accounts Payable', 2, 201, 'Amounts owed to suppliers and vendors', 0, 1, 8, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '2300', 'Loans Payable', 2, 203, 'Borrowed funds and loans', 0, 1, 9, 1, @Now, 'System');

            -- ===== EQUITY (3000-3999) =====
            INSERT INTO LedgerAccounts (Id, QId, AccountCode, AccountName, Category, [Type], Description, IsDebitNormal, IsSystemAccount, DisplayOrder, IsActive, DateCreated, CreatedBy)
            VALUES
            (NEWID(), @QuarryId, '3000', 'Owner''s Equity', 3, 300, 'Owner''s capital investment in the quarry', 0, 1, 10, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '3100', 'Retained Earnings', 3, 301, 'Accumulated profits from prior periods', 0, 1, 11, 1, @Now, 'System');

            -- ===== REVENUE (4000-4999) =====
            INSERT INTO LedgerAccounts (Id, QId, AccountCode, AccountName, Category, [Type], Description, IsDebitNormal, IsSystemAccount, DisplayOrder, IsActive, DateCreated, CreatedBy)
            VALUES
            (NEWID(), @QuarryId, '4000', 'Sales Revenue', 4, 400, 'Total income from all product sales', 0, 1, 12, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '4010', 'Sales - Size 6', 4, 400, 'Revenue from Size 6 ballast sales', 0, 1, 13, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '4020', 'Sales - Size 9', 4, 400, 'Revenue from Size 9 ballast sales', 0, 1, 14, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '4030', 'Sales - Size 4', 4, 400, 'Revenue from Size 4 ballast sales', 0, 1, 15, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '4040', 'Sales - Reject', 4, 400, 'Revenue from Reject product sales', 0, 1, 16, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '4050', 'Sales - Hardcore', 4, 400, 'Revenue from Hardcore product sales', 0, 1, 17, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '4060', 'Sales - Beam', 4, 400, 'Revenue from Beam product sales', 0, 1, 18, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '4500', 'Other Income', 4, 401, 'Miscellaneous income sources', 0, 1, 19, 1, @Now, 'System');

            -- ===== COST OF SALES (5000-5999) =====
            INSERT INTO LedgerAccounts (Id, QId, AccountCode, AccountName, Category, [Type], Description, IsDebitNormal, IsSystemAccount, DisplayOrder, IsActive, DateCreated, CreatedBy)
            VALUES
            (NEWID(), @QuarryId, '5000', 'Commission Expense', 5, 500, 'Broker commissions on sales', 1, 1, 20, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '5100', 'Loaders Fees', 5, 501, 'Per-unit fees for loaders', 1, 1, 21, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '5200', 'Land Rate Fees', 5, 502, 'Per-unit land rate charges', 1, 1, 22, 1, @Now, 'System');

            -- ===== OPERATING EXPENSES (6000-6999) =====
            INSERT INTO LedgerAccounts (Id, QId, AccountCode, AccountName, Category, [Type], Description, IsDebitNormal, IsSystemAccount, DisplayOrder, IsActive, DateCreated, CreatedBy)
            VALUES
            (NEWID(), @QuarryId, '6000', 'Fuel Expense', 6, 600, 'Fuel costs for machines and operations', 1, 1, 23, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6100', 'Transportation Hire', 6, 601, 'Hired transport costs', 1, 1, 24, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6200', 'Maintenance and Repairs', 6, 602, 'Equipment maintenance and repairs', 1, 1, 25, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6300', 'Consumables and Utilities', 6, 603, 'Operational supplies and utilities', 1, 1, 26, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6400', 'Administrative Expenses', 6, 604, 'Office and administrative costs', 1, 1, 27, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6500', 'Marketing Expenses', 6, 605, 'Advertising and promotional costs', 1, 1, 28, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6600', 'Wages and Salaries', 6, 606, 'Employee compensation and wages', 1, 1, 29, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6700', 'Bank Charges', 6, 607, 'Banking fees and transaction charges', 1, 1, 30, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6800', 'Cess and Road Fees', 6, 608, 'Government levies and road fees', 1, 1, 31, 1, @Now, 'System'),
            (NEWID(), @QuarryId, '6900', 'Miscellaneous Expenses', 6, 609, 'Other operational costs not classified elsewhere', 1, 1, 32, 1, @Now, 'System');

            SET @QuarryCount = @QuarryCount + 1;
            PRINT '  -> Seeded 32 accounts for ' + @QuarryName;
        END
        ELSE
        BEGIN
            SET @SkippedCount = @SkippedCount + 1;
            PRINT 'Skipping ' + @QuarryName + ' - accounts already exist';
        END

        FETCH NEXT FROM QuarryCursor INTO @QuarryId, @QuarryName;
    END

    CLOSE QuarryCursor;
    DEALLOCATE QuarryCursor;

    COMMIT TRANSACTION;

    PRINT '';
    PRINT '=============================================';
    PRINT 'CHART OF ACCOUNTS SEEDING COMPLETED';
    PRINT '=============================================';
    PRINT 'Quarries seeded: ' + CAST(@QuarryCount AS VARCHAR(10));
    PRINT 'Quarries skipped (already had accounts): ' + CAST(@SkippedCount AS VARCHAR(10));
    PRINT '=============================================';

    -- Verify the seeding
    SELECT
        q.QuarryName,
        COUNT(la.Id) AS AccountCount
    FROM Quarries q
    LEFT JOIN LedgerAccounts la ON la.QId = q.Id
    WHERE q.IsActive = 1
    GROUP BY q.Id, q.QuarryName
    ORDER BY q.QuarryName;

END TRY
BEGIN CATCH
    -- Clean up cursor if still open
    IF CURSOR_STATUS('global', 'QuarryCursor') >= 0
        CLOSE QuarryCursor;
    IF CURSOR_STATUS('global', 'QuarryCursor') >= -1
        DEALLOCATE QuarryCursor;

    -- Rollback transaction if active
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '';
    PRINT '=============================================';
    PRINT 'ERROR: CHART OF ACCOUNTS SEEDING FAILED!';
    PRINT '=============================================';
    PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10));
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS NVARCHAR(10));
    PRINT '';
    PRINT 'All changes have been rolled back.';
    PRINT 'Please fix the error and re-run the script.';
    PRINT '=============================================';

    -- Re-throw the error
    THROW;
END CATCH;
