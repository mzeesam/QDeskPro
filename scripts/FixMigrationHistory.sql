-- =============================================================================
-- PRODUCTION-SAFE SCRIPT: Fix EF Core Migration History
-- =============================================================================
-- This script ensures all existing migrations are recorded in __EFMigrationsHistory
-- Run this BEFORE deploying new code that includes auto-migrations
--
-- This is SAFE to run multiple times - it only inserts missing records
-- =============================================================================

-- Ensure the migration history table exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
    PRINT 'Created __EFMigrationsHistory table';
END

-- Insert missing migration records (only if they don't exist)
DECLARE @ProductVersion nvarchar(32) = '10.0.0-rc.2.25113.107';

-- InitialCreate
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251218173048_InitialCreate')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251218173048_InitialCreate', @ProductVersion);
    PRINT 'Inserted: 20251218173048_InitialCreate';
END

-- AddAIEntities
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251221123646_AddAIEntities')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251221123646_AddAIEntities', @ProductVersion);
    PRINT 'Inserted: 20251221123646_AddAIEntities';
END

-- AddRefreshTokens
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251224095026_AddRefreshTokens')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251224095026_AddRefreshTokens', @ProductVersion);
    PRINT 'Inserted: 20251224095026_AddRefreshTokens';
END

-- AddPaymentReceivedDateToSale
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251225155920_AddPaymentReceivedDateToSale')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251225155920_AddPaymentReceivedDateToSale', @ProductVersion);
    PRINT 'Inserted: 20251225155920_AddPaymentReceivedDateToSale';
END

-- SyncModelChanges
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251226082459_SyncModelChanges')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251226082459_SyncModelChanges', @ProductVersion);
    PRINT 'Inserted: 20251226082459_SyncModelChanges';
END

-- AddIncludeLandRateToSale
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251226100529_AddIncludeLandRateToSale')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251226100529_AddIncludeLandRateToSale', @ProductVersion);
    PRINT 'Inserted: 20251226100529_AddIncludeLandRateToSale';
END

-- AddPrepaymentSupport
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251227201457_AddPrepaymentSupport')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251227201457_AddPrepaymentSupport', @ProductVersion);
    PRINT 'Inserted: 20251227201457_AddPrepaymentSupport';
END

-- AddManagerHierarchy
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251228183351_AddManagerHierarchy')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251228183351_AddManagerHierarchy', @ProductVersion);
    PRINT 'Inserted: 20251228183351_AddManagerHierarchy';
END

-- AddAccountingModule
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20251230163444_AddAccountingModule')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251230163444_AddAccountingModule', @ProductVersion);
    PRINT 'Inserted: 20251230163444_AddAccountingModule';
END

-- Verify the result
PRINT '';
PRINT 'Current migration history:';
SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;
