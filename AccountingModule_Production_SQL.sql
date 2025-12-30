-- =====================================================
-- QDeskPro Accounting Module - Production SQL Script
-- Version: 1.0
-- Date: 2025-12-30
-- Description: Creates accounting tables for financial reporting
-- =====================================================

-- Run this script on your production database
-- Before running, ensure you have a backup of your database

BEGIN TRANSACTION;

-- =====================================================
-- 1. Create LedgerAccounts table (Chart of Accounts)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LedgerAccounts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[LedgerAccounts] (
        [Id] NVARCHAR(450) NOT NULL,
        [AccountCode] NVARCHAR(20) NOT NULL,
        [AccountName] NVARCHAR(200) NOT NULL,
        [Category] INT NOT NULL,              -- 1=Assets, 2=Liabilities, 3=Equity, 4=Revenue, 5=CostOfSales, 6=Expenses
        [Type] INT NOT NULL,                  -- Specific account type enum value
        [ParentAccountId] NVARCHAR(450) NULL,
        [IsSystemAccount] BIT NOT NULL DEFAULT 0,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [Description] NVARCHAR(500) NULL,
        [IsDebitNormal] BIT NOT NULL DEFAULT 1,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [DateCreated] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(MAX) NULL,
        [DateModified] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(MAX) NULL,
        [DateStamp] NVARCHAR(MAX) NULL,
        [QId] NVARCHAR(450) NULL,
        CONSTRAINT [PK_LedgerAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LedgerAccounts_LedgerAccounts_ParentAccountId]
            FOREIGN KEY ([ParentAccountId]) REFERENCES [dbo].[LedgerAccounts] ([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table: LedgerAccounts';
END
ELSE
BEGIN
    PRINT 'Table LedgerAccounts already exists';
END
GO

-- =====================================================
-- 2. Create JournalEntries table
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[JournalEntries]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[JournalEntries] (
        [Id] NVARCHAR(450) NOT NULL,
        [EntryDate] DATETIME2 NOT NULL,
        [Reference] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(500) NOT NULL,
        [EntryType] NVARCHAR(20) NOT NULL DEFAULT 'Auto',  -- 'Auto' or 'Manual'
        [SourceEntityType] NVARCHAR(50) NULL,              -- 'Sale', 'Expense', 'Banking', etc.
        [SourceEntityId] NVARCHAR(50) NULL,
        [IsPosted] BIT NOT NULL DEFAULT 0,
        [PostedBy] NVARCHAR(MAX) NULL,
        [PostedDate] DATETIME2 NULL,
        [TotalDebit] FLOAT NOT NULL DEFAULT 0,
        [TotalCredit] FLOAT NOT NULL DEFAULT 0,
        [FiscalYear] INT NOT NULL,
        [FiscalPeriod] INT NOT NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [DateCreated] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(MAX) NULL,
        [DateModified] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(MAX) NULL,
        [DateStamp] NVARCHAR(MAX) NULL,
        [QId] NVARCHAR(450) NULL,
        CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id])
    );
    PRINT 'Created table: JournalEntries';
END
ELSE
BEGIN
    PRINT 'Table JournalEntries already exists';
END
GO

-- =====================================================
-- 3. Create JournalEntryLines table
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[JournalEntryLines]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[JournalEntryLines] (
        [Id] NVARCHAR(450) NOT NULL,
        [JournalEntryId] NVARCHAR(450) NOT NULL,
        [LedgerAccountId] NVARCHAR(450) NOT NULL,
        [DebitAmount] FLOAT NOT NULL DEFAULT 0,
        [CreditAmount] FLOAT NOT NULL DEFAULT 0,
        [Memo] NVARCHAR(500) NULL,
        [LineNumber] INT NOT NULL DEFAULT 0,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [DateCreated] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(MAX) NULL,
        [DateModified] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(MAX) NULL,
        [DateStamp] NVARCHAR(MAX) NULL,
        [QId] NVARCHAR(MAX) NULL,
        CONSTRAINT [PK_JournalEntryLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalEntryLines_JournalEntries_JournalEntryId]
            FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_JournalEntryLines_LedgerAccounts_LedgerAccountId]
            FOREIGN KEY ([LedgerAccountId]) REFERENCES [dbo].[LedgerAccounts] ([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table: JournalEntryLines';
END
ELSE
BEGIN
    PRINT 'Table JournalEntryLines already exists';
END
GO

-- =====================================================
-- 4. Create AccountingPeriods table
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AccountingPeriods]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AccountingPeriods] (
        [Id] NVARCHAR(450) NOT NULL,
        [PeriodName] NVARCHAR(100) NOT NULL,
        [StartDate] DATETIME2 NOT NULL,
        [EndDate] DATETIME2 NOT NULL,
        [IsClosed] BIT NOT NULL DEFAULT 0,
        [ClosedBy] NVARCHAR(MAX) NULL,
        [ClosedDate] DATETIME2 NULL,
        [FiscalYear] INT NOT NULL,
        [PeriodNumber] INT NOT NULL,
        [PeriodType] NVARCHAR(20) NOT NULL DEFAULT 'Monthly',
        [ClosingNotes] NVARCHAR(1000) NULL,
        [IsActive] BIT NOT NULL DEFAULT 1,
        [DateCreated] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] NVARCHAR(MAX) NULL,
        [DateModified] DATETIME2 NULL,
        [ModifiedBy] NVARCHAR(MAX) NULL,
        [DateStamp] NVARCHAR(MAX) NULL,
        [QId] NVARCHAR(450) NULL,
        CONSTRAINT [PK_AccountingPeriods] PRIMARY KEY ([Id])
    );
    PRINT 'Created table: AccountingPeriods';
END
ELSE
BEGIN
    PRINT 'Table AccountingPeriods already exists';
END
GO

-- =====================================================
-- 5. Create Indexes for LedgerAccounts
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_AccountCode')
    CREATE INDEX [IX_LedgerAccounts_AccountCode] ON [dbo].[LedgerAccounts] ([AccountCode]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_QId')
    CREATE INDEX [IX_LedgerAccounts_QId] ON [dbo].[LedgerAccounts] ([QId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_Category')
    CREATE INDEX [IX_LedgerAccounts_Category] ON [dbo].[LedgerAccounts] ([Category]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_ParentAccountId')
    CREATE INDEX [IX_LedgerAccounts_ParentAccountId] ON [dbo].[LedgerAccounts] ([ParentAccountId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_QId_AccountCode')
    CREATE UNIQUE INDEX [IX_LedgerAccounts_QId_AccountCode] ON [dbo].[LedgerAccounts] ([QId], [AccountCode]) WHERE [QId] IS NOT NULL;

PRINT 'Created indexes for LedgerAccounts';
GO

-- =====================================================
-- 6. Create Indexes for JournalEntries
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_Reference')
    CREATE INDEX [IX_JournalEntries_Reference] ON [dbo].[JournalEntries] ([Reference]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_EntryDate')
    CREATE INDEX [IX_JournalEntries_EntryDate] ON [dbo].[JournalEntries] ([EntryDate]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_QId')
    CREATE INDEX [IX_JournalEntries_QId] ON [dbo].[JournalEntries] ([QId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_FiscalYear')
    CREATE INDEX [IX_JournalEntries_FiscalYear] ON [dbo].[JournalEntries] ([FiscalYear]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_FiscalYear_FiscalPeriod')
    CREATE INDEX [IX_JournalEntries_FiscalYear_FiscalPeriod] ON [dbo].[JournalEntries] ([FiscalYear], [FiscalPeriod]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_SourceEntityType_SourceEntityId')
    CREATE INDEX [IX_JournalEntries_SourceEntityType_SourceEntityId] ON [dbo].[JournalEntries] ([SourceEntityType], [SourceEntityId]);

PRINT 'Created indexes for JournalEntries';
GO

-- =====================================================
-- 7. Create Indexes for JournalEntryLines
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntryLines_JournalEntryId')
    CREATE INDEX [IX_JournalEntryLines_JournalEntryId] ON [dbo].[JournalEntryLines] ([JournalEntryId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntryLines_LedgerAccountId')
    CREATE INDEX [IX_JournalEntryLines_LedgerAccountId] ON [dbo].[JournalEntryLines] ([LedgerAccountId]);

PRINT 'Created indexes for JournalEntryLines';
GO

-- =====================================================
-- 8. Create Indexes for AccountingPeriods
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccountingPeriods_QId')
    CREATE INDEX [IX_AccountingPeriods_QId] ON [dbo].[AccountingPeriods] ([QId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccountingPeriods_FiscalYear')
    CREATE INDEX [IX_AccountingPeriods_FiscalYear] ON [dbo].[AccountingPeriods] ([FiscalYear]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccountingPeriods_QId_FiscalYear_PeriodNumber')
    CREATE UNIQUE INDEX [IX_AccountingPeriods_QId_FiscalYear_PeriodNumber] ON [dbo].[AccountingPeriods] ([QId], [FiscalYear], [PeriodNumber]) WHERE [QId] IS NOT NULL;

PRINT 'Created indexes for AccountingPeriods';
GO

-- =====================================================
-- 9. Insert migration record (optional - for EF Core tracking)
-- =====================================================
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251230163444_AddAccountingModule')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251230163444_AddAccountingModule', '9.0.0');
    PRINT 'Recorded migration in __EFMigrationsHistory';
END
GO

COMMIT TRANSACTION;

PRINT '';
PRINT '=====================================================';
PRINT 'Accounting Module migration completed successfully!';
PRINT '=====================================================';
PRINT '';
PRINT 'Tables created:';
PRINT '  - LedgerAccounts (Chart of Accounts)';
PRINT '  - JournalEntries (Double-entry bookkeeping)';
PRINT '  - JournalEntryLines (Debit/Credit lines)';
PRINT '  - AccountingPeriods (Fiscal periods)';
PRINT '';
PRINT 'Next step: Run the Chart of Accounts seed script';
PRINT '=====================================================';
GO
