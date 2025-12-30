-- =============================================
-- QDeskPro Accounting Module Migration Script
-- Generated: 2024-12-30
-- Migration: 20251230163444_AddAccountingModule
-- =============================================
-- Run this script on production SQL Server to add accounting tables
-- =============================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;

-- =============================================
-- 1. CREATE ACCOUNTING PERIODS TABLE
-- =============================================
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
    PRINT 'Table AccountingPeriods already exists - skipping';

-- =============================================
-- 2. CREATE LEDGER ACCOUNTS TABLE
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[LedgerAccounts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[LedgerAccounts] (
        [Id] NVARCHAR(450) NOT NULL,
        [AccountCode] NVARCHAR(20) NOT NULL,
        [AccountName] NVARCHAR(200) NOT NULL,
        [Category] INT NOT NULL,
        [Type] INT NOT NULL,
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
        CONSTRAINT [FK_LedgerAccounts_LedgerAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId])
            REFERENCES [dbo].[LedgerAccounts] ([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table: LedgerAccounts';
END
ELSE
    PRINT 'Table LedgerAccounts already exists - skipping';

-- =============================================
-- 3. CREATE JOURNAL ENTRIES TABLE
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[JournalEntries]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[JournalEntries] (
        [Id] NVARCHAR(450) NOT NULL,
        [EntryDate] DATETIME2 NOT NULL,
        [Reference] NVARCHAR(50) NOT NULL,
        [Description] NVARCHAR(500) NOT NULL,
        [EntryType] NVARCHAR(20) NOT NULL,
        [SourceEntityType] NVARCHAR(50) NULL,
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
    PRINT 'Table JournalEntries already exists - skipping';

-- =============================================
-- 4. CREATE JOURNAL ENTRY LINES TABLE
-- =============================================
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
        CONSTRAINT [FK_JournalEntryLines_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId])
            REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_JournalEntryLines_LedgerAccounts_LedgerAccountId] FOREIGN KEY ([LedgerAccountId])
            REFERENCES [dbo].[LedgerAccounts] ([Id]) ON DELETE NO ACTION
    );
    PRINT 'Created table: JournalEntryLines';
END
ELSE
    PRINT 'Table JournalEntryLines already exists - skipping';

-- =============================================
-- 5. CREATE INDEXES
-- =============================================

-- AccountingPeriods indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccountingPeriods_FiscalYear')
    CREATE INDEX [IX_AccountingPeriods_FiscalYear] ON [dbo].[AccountingPeriods] ([FiscalYear]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccountingPeriods_QId')
    CREATE INDEX [IX_AccountingPeriods_QId] ON [dbo].[AccountingPeriods] ([QId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AccountingPeriods_QId_FiscalYear_PeriodNumber')
    CREATE UNIQUE INDEX [IX_AccountingPeriods_QId_FiscalYear_PeriodNumber]
    ON [dbo].[AccountingPeriods] ([QId], [FiscalYear], [PeriodNumber])
    WHERE [QId] IS NOT NULL;

-- JournalEntries indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_EntryDate')
    CREATE INDEX [IX_JournalEntries_EntryDate] ON [dbo].[JournalEntries] ([EntryDate]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_FiscalYear')
    CREATE INDEX [IX_JournalEntries_FiscalYear] ON [dbo].[JournalEntries] ([FiscalYear]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_FiscalYear_FiscalPeriod')
    CREATE INDEX [IX_JournalEntries_FiscalYear_FiscalPeriod] ON [dbo].[JournalEntries] ([FiscalYear], [FiscalPeriod]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_QId')
    CREATE INDEX [IX_JournalEntries_QId] ON [dbo].[JournalEntries] ([QId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_Reference')
    CREATE INDEX [IX_JournalEntries_Reference] ON [dbo].[JournalEntries] ([Reference]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntries_SourceEntityType_SourceEntityId')
    CREATE INDEX [IX_JournalEntries_SourceEntityType_SourceEntityId] ON [dbo].[JournalEntries] ([SourceEntityType], [SourceEntityId]);

-- JournalEntryLines indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntryLines_JournalEntryId')
    CREATE INDEX [IX_JournalEntryLines_JournalEntryId] ON [dbo].[JournalEntryLines] ([JournalEntryId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_JournalEntryLines_LedgerAccountId')
    CREATE INDEX [IX_JournalEntryLines_LedgerAccountId] ON [dbo].[JournalEntryLines] ([LedgerAccountId]);

-- LedgerAccounts indexes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_AccountCode')
    CREATE INDEX [IX_LedgerAccounts_AccountCode] ON [dbo].[LedgerAccounts] ([AccountCode]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_Category')
    CREATE INDEX [IX_LedgerAccounts_Category] ON [dbo].[LedgerAccounts] ([Category]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_ParentAccountId')
    CREATE INDEX [IX_LedgerAccounts_ParentAccountId] ON [dbo].[LedgerAccounts] ([ParentAccountId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_QId')
    CREATE INDEX [IX_LedgerAccounts_QId] ON [dbo].[LedgerAccounts] ([QId]);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_LedgerAccounts_QId_AccountCode')
    CREATE UNIQUE INDEX [IX_LedgerAccounts_QId_AccountCode]
    ON [dbo].[LedgerAccounts] ([QId], [AccountCode])
    WHERE [QId] IS NOT NULL;

PRINT 'Created indexes';

-- =============================================
-- 6. RECORD MIGRATION IN EF HISTORY
-- =============================================
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20251230163444_AddAccountingModule')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20251230163444_AddAccountingModule', '9.0.0');
    PRINT 'Recorded migration in __EFMigrationsHistory';
END
ELSE
    PRINT 'Migration already recorded in __EFMigrationsHistory - skipping';

COMMIT TRANSACTION;

PRINT '';
PRINT '=============================================';
PRINT 'ACCOUNTING MODULE MIGRATION COMPLETED';
PRINT '=============================================';
PRINT '';
PRINT 'Tables created:';
PRINT '  - AccountingPeriods';
PRINT '  - LedgerAccounts';
PRINT '  - JournalEntries';
PRINT '  - JournalEntryLines';
PRINT '';
PRINT 'Next step: Seed the Chart of Accounts using the application';
PRINT 'or run the SeedChartOfAccounts.sql script';
GO
