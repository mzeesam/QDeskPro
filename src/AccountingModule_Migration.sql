IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(max) NULL,
        [Position] nvarchar(max) NULL,
        [QuarryId] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [Bankings] (
        [Id] nvarchar(450) NOT NULL,
        [BankingDate] datetime2 NULL,
        [Item] nvarchar(500) NULL,
        [BalanceBF] float NOT NULL,
        [AmountBanked] float NOT NULL,
        [TxnReference] nvarchar(max) NULL,
        [RefCode] nvarchar(20) NULL,
        [ApplicationUserId] nvarchar(450) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(450) NULL,
        [QId] nvarchar(450) NULL,
        CONSTRAINT [PK_Bankings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [DailyNotes] (
        [Id] nvarchar(450) NOT NULL,
        [NoteDate] datetime2 NULL,
        [Notes] nvarchar(1000) NULL,
        [ClosingBalance] float NOT NULL,
        [quarryId] nvarchar(450) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(450) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_DailyNotes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [Expenses] (
        [Id] nvarchar(450) NOT NULL,
        [ExpenseDate] datetime2 NULL,
        [Item] nvarchar(500) NOT NULL,
        [Amount] float NOT NULL,
        [Category] nvarchar(100) NULL,
        [TxnReference] nvarchar(max) NULL,
        [ApplicationUserId] nvarchar(450) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(450) NULL,
        [QId] nvarchar(450) NULL,
        CONSTRAINT [PK_Expenses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [FuelUsages] (
        [Id] nvarchar(450) NOT NULL,
        [UsageDate] datetime2 NULL,
        [OldStock] float NOT NULL,
        [NewStock] float NOT NULL,
        [MachinesLoaded] float NOT NULL,
        [WheelLoadersLoaded] float NOT NULL,
        [ApplicationUserId] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(450) NULL,
        [QId] nvarchar(450) NULL,
        CONSTRAINT [PK_FuelUsages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [Products] (
        [Id] nvarchar(450) NOT NULL,
        [ProductName] nvarchar(100) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [Quarries] (
        [Id] nvarchar(450) NOT NULL,
        [QuarryName] nvarchar(200) NOT NULL,
        [Location] nvarchar(500) NULL,
        [ManagerId] nvarchar(450) NULL,
        [LoadersFee] float NULL,
        [LandRateFee] float NULL,
        [RejectsFee] float NULL,
        [EmailRecipients] nvarchar(max) NULL,
        [DailyReportEnabled] bit NOT NULL,
        [DailyReportTime] time NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_Quarries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Quarries_AspNetUsers_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [Brokers] (
        [Id] nvarchar(450) NOT NULL,
        [BrokerName] nvarchar(200) NOT NULL,
        [Phone] nvarchar(max) NULL,
        [quarryId] nvarchar(450) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_Brokers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Brokers_Quarries_quarryId] FOREIGN KEY ([quarryId]) REFERENCES [Quarries] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [Layers] (
        [Id] nvarchar(450) NOT NULL,
        [LayerLevel] nvarchar(100) NOT NULL,
        [DateStarted] datetime2 NULL,
        [LayerLength] float NULL,
        [QuarryId] nvarchar(450) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_Layers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Layers_Quarries_QuarryId] FOREIGN KEY ([QuarryId]) REFERENCES [Quarries] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [ProductPrices] (
        [Id] nvarchar(450) NOT NULL,
        [ProductId] nvarchar(450) NULL,
        [QuarryId] nvarchar(450) NULL,
        [Price] float NOT NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductPrices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductPrices_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProductPrices_Quarries_QuarryId] FOREIGN KEY ([QuarryId]) REFERENCES [Quarries] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [UserQuarries] (
        [Id] nvarchar(450) NOT NULL,
        [UserId] nvarchar(450) NULL,
        [QuarryId] nvarchar(450) NULL,
        [IsPrimary] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_UserQuarries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserQuarries_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UserQuarries_Quarries_QuarryId] FOREIGN KEY ([QuarryId]) REFERENCES [Quarries] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE TABLE [Sales] (
        [Id] nvarchar(450) NOT NULL,
        [SaleDate] datetime2 NULL,
        [VehicleRegistration] nvarchar(20) NOT NULL,
        [ClientName] nvarchar(max) NULL,
        [ClientPhone] nvarchar(max) NULL,
        [Destination] nvarchar(max) NULL,
        [ProductId] nvarchar(450) NULL,
        [LayerId] nvarchar(450) NULL,
        [Quantity] float NOT NULL,
        [PricePerUnit] float NOT NULL,
        [BrokerId] nvarchar(450) NULL,
        [CommissionPerUnit] float NOT NULL,
        [PaymentStatus] nvarchar(20) NULL,
        [PaymentMode] nvarchar(20) NULL,
        [PaymentReference] nvarchar(max) NULL,
        [ApplicationUserId] nvarchar(450) NULL,
        [ClerkName] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(450) NULL,
        [QId] nvarchar(450) NULL,
        CONSTRAINT [PK_Sales] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sales_AspNetUsers_ApplicationUserId] FOREIGN KEY ([ApplicationUserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Sales_Brokers_BrokerId] FOREIGN KEY ([BrokerId]) REFERENCES [Brokers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Sales_Layers_LayerId] FOREIGN KEY ([LayerId]) REFERENCES [Layers] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Sales_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bankings_ApplicationUserId] ON [Bankings] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bankings_DateStamp] ON [Bankings] ([DateStamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bankings_QId] ON [Bankings] ([QId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Brokers_quarryId] ON [Brokers] ([quarryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DailyNotes_DateStamp] ON [DailyNotes] ([DateStamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DailyNotes_quarryId] ON [DailyNotes] ([quarryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_ApplicationUserId] ON [Expenses] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_DateStamp] ON [Expenses] ([DateStamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Expenses_QId] ON [Expenses] ([QId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FuelUsages_DateStamp] ON [FuelUsages] ([DateStamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FuelUsages_QId] ON [FuelUsages] ([QId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Layers_QuarryId] ON [Layers] ([QuarryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductPrices_ProductId_QuarryId] ON [ProductPrices] ([ProductId], [QuarryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ProductPrices_QuarryId] ON [ProductPrices] ([QuarryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Quarries_ManagerId] ON [Quarries] ([ManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sales_ApplicationUserId] ON [Sales] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sales_BrokerId] ON [Sales] ([BrokerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sales_DateStamp] ON [Sales] ([DateStamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sales_LayerId] ON [Sales] ([LayerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sales_ProductId] ON [Sales] ([ProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sales_QId] ON [Sales] ([QId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserQuarries_QuarryId] ON [UserQuarries] ([QuarryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_UserQuarries_UserId_QuarryId] ON [UserQuarries] ([UserId], [QuarryId]) WHERE [UserId] IS NOT NULL AND [QuarryId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251218173048_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251218173048_InitialCreate', N'9.0.1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE TABLE [AIConversations] (
        [Id] nvarchar(450) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [ChatType] nvarchar(50) NOT NULL,
        [QuarryId] nvarchar(450) NULL,
        [LastMessageAt] datetime2 NOT NULL,
        [TotalTokensUsed] int NOT NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_AIConversations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AIConversations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AIConversations_Quarries_QuarryId] FOREIGN KEY ([QuarryId]) REFERENCES [Quarries] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE TABLE [PushSubscriptions] (
        [Id] nvarchar(450) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Endpoint] nvarchar(500) NOT NULL,
        [P256dh] nvarchar(200) NOT NULL,
        [Auth] nvarchar(200) NOT NULL,
        [SubscribedAt] datetime2 NOT NULL,
        [UserAgent] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_PushSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PushSubscriptions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE TABLE [AIMessages] (
        [Id] nvarchar(450) NOT NULL,
        [AIConversationId] nvarchar(450) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ToolCallId] nvarchar(max) NULL,
        [ToolName] nvarchar(100) NULL,
        [ToolResult] nvarchar(max) NULL,
        [TokensUsed] int NULL,
        [Timestamp] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_AIMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AIMessages_AIConversations_AIConversationId] FOREIGN KEY ([AIConversationId]) REFERENCES [AIConversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE INDEX [IX_AIConversations_LastMessageAt] ON [AIConversations] ([LastMessageAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE INDEX [IX_AIConversations_QuarryId] ON [AIConversations] ([QuarryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE INDEX [IX_AIConversations_UserId] ON [AIConversations] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE INDEX [IX_AIMessages_AIConversationId] ON [AIMessages] ([AIConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE INDEX [IX_AIMessages_Timestamp] ON [AIMessages] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PushSubscriptions_Endpoint] ON [PushSubscriptions] ([Endpoint]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    CREATE INDEX [IX_PushSubscriptions_UserId] ON [PushSubscriptions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251221123646_AddAIEntities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251221123646_AddAIEntities', N'9.0.1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251224095026_AddRefreshTokens'
)
BEGIN
    DROP TABLE [PushSubscriptions];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251224095026_AddRefreshTokens'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] nvarchar(450) NOT NULL,
        [Token] nvarchar(500) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL,
        [RevokedAt] datetime2 NULL,
        [ReplacedByToken] nvarchar(max) NULL,
        [DeviceInfo] nvarchar(500) NULL,
        [IpAddress] nvarchar(45) NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251224095026_AddRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_ExpiresAt] ON [RefreshTokens] ([ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251224095026_AddRefreshTokens'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [RefreshTokens] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251224095026_AddRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251224095026_AddRefreshTokens'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId_IsRevoked_ExpiresAt] ON [RefreshTokens] ([UserId], [IsRevoked], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251224095026_AddRefreshTokens'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251224095026_AddRefreshTokens', N'9.0.1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251225155920_AddPaymentReceivedDateToSale'
)
BEGIN
    ALTER TABLE [Sales] ADD [PaymentReceivedDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251225155920_AddPaymentReceivedDateToSale'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251225155920_AddPaymentReceivedDateToSale', N'9.0.1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226082459_SyncModelChanges'
)
BEGIN
    ALTER TABLE [Quarries] ADD [DailyProductionCapacity] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226082459_SyncModelChanges'
)
BEGIN
    ALTER TABLE [Quarries] ADD [EstimatedMonthlyFixedCosts] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226082459_SyncModelChanges'
)
BEGIN
    ALTER TABLE [Quarries] ADD [FuelCostPerLiter] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226082459_SyncModelChanges'
)
BEGIN
    ALTER TABLE [Quarries] ADD [InitialCapitalInvestment] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226082459_SyncModelChanges'
)
BEGIN
    ALTER TABLE [Quarries] ADD [OperationsStartDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226082459_SyncModelChanges'
)
BEGIN
    ALTER TABLE [Quarries] ADD [TargetProfitMargin] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226082459_SyncModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251226082459_SyncModelChanges', N'9.0.1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226100529_AddIncludeLandRateToSale'
)
BEGIN
    ALTER TABLE [Sales] ADD [IncludeLandRate] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251226100529_AddIncludeLandRateToSale'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251226100529_AddIncludeLandRateToSale', N'9.0.1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    ALTER TABLE [Sales] ADD [IsPrepaymentSale] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    ALTER TABLE [Sales] ADD [PrepaymentApplied] float NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    ALTER TABLE [Sales] ADD [PrepaymentId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE TABLE [Prepayments] (
        [Id] nvarchar(450) NOT NULL,
        [VehicleRegistration] nvarchar(50) NOT NULL,
        [ClientName] nvarchar(200) NULL,
        [ClientPhone] nvarchar(20) NULL,
        [PrepaymentDate] datetime2 NOT NULL,
        [TotalAmountPaid] float NOT NULL,
        [AmountUsed] float NOT NULL,
        [IntendedProductId] nvarchar(450) NULL,
        [IntendedQuantity] float NULL,
        [IntendedPricePerUnit] float NULL,
        [PaymentMode] nvarchar(50) NOT NULL,
        [PaymentReference] nvarchar(200) NULL,
        [Status] nvarchar(20) NOT NULL,
        [FullyFulfilledDate] datetime2 NULL,
        [ApplicationUserId] nvarchar(450) NOT NULL,
        [ClerkName] nvarchar(200) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(450) NULL,
        [QId] nvarchar(450) NULL,
        CONSTRAINT [PK_Prepayments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Prepayments_Products_IntendedProductId] FOREIGN KEY ([IntendedProductId]) REFERENCES [Products] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE INDEX [IX_Sales_PrepaymentId] ON [Sales] ([PrepaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE INDEX [IX_Prepayments_ApplicationUserId] ON [Prepayments] ([ApplicationUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE INDEX [IX_Prepayments_DateStamp] ON [Prepayments] ([DateStamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE INDEX [IX_Prepayments_IntendedProductId] ON [Prepayments] ([IntendedProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE INDEX [IX_Prepayments_PrepaymentDate] ON [Prepayments] ([PrepaymentDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE INDEX [IX_Prepayments_QId] ON [Prepayments] ([QId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE INDEX [IX_Prepayments_Status] ON [Prepayments] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    CREATE INDEX [IX_Prepayments_VehicleRegistration] ON [Prepayments] ([VehicleRegistration]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    ALTER TABLE [Sales] ADD CONSTRAINT [FK_Sales_Prepayments_PrepaymentId] FOREIGN KEY ([PrepaymentId]) REFERENCES [Prepayments] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251227201457_AddPrepaymentSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251227201457_AddPrepaymentSupport', N'9.0.1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228183351_AddManagerHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD [CreatedByManagerId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228183351_AddManagerHierarchy'
)
BEGIN
    CREATE INDEX [IX_AspNetUsers_CreatedByManagerId] ON [AspNetUsers] ([CreatedByManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228183351_AddManagerHierarchy'
)
BEGIN
    ALTER TABLE [AspNetUsers] ADD CONSTRAINT [FK_AspNetUsers_AspNetUsers_CreatedByManagerId] FOREIGN KEY ([CreatedByManagerId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251228183351_AddManagerHierarchy'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251228183351_AddManagerHierarchy', N'9.0.1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE TABLE [AccountingPeriods] (
        [Id] nvarchar(450) NOT NULL,
        [PeriodName] nvarchar(100) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [IsClosed] bit NOT NULL,
        [ClosedBy] nvarchar(max) NULL,
        [ClosedDate] datetime2 NULL,
        [FiscalYear] int NOT NULL,
        [PeriodNumber] int NOT NULL,
        [PeriodType] nvarchar(20) NOT NULL,
        [ClosingNotes] nvarchar(1000) NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(450) NULL,
        CONSTRAINT [PK_AccountingPeriods] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE TABLE [JournalEntries] (
        [Id] nvarchar(450) NOT NULL,
        [EntryDate] datetime2 NOT NULL,
        [Reference] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [EntryType] nvarchar(20) NOT NULL,
        [SourceEntityType] nvarchar(50) NULL,
        [SourceEntityId] nvarchar(50) NULL,
        [IsPosted] bit NOT NULL,
        [PostedBy] nvarchar(max) NULL,
        [PostedDate] datetime2 NULL,
        [TotalDebit] float NOT NULL,
        [TotalCredit] float NOT NULL,
        [FiscalYear] int NOT NULL,
        [FiscalPeriod] int NOT NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(450) NULL,
        CONSTRAINT [PK_JournalEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE TABLE [LedgerAccounts] (
        [Id] nvarchar(450) NOT NULL,
        [AccountCode] nvarchar(20) NOT NULL,
        [AccountName] nvarchar(200) NOT NULL,
        [Category] int NOT NULL,
        [Type] int NOT NULL,
        [ParentAccountId] nvarchar(450) NULL,
        [IsSystemAccount] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsDebitNormal] bit NOT NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(450) NULL,
        CONSTRAINT [PK_LedgerAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LedgerAccounts_LedgerAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [LedgerAccounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE TABLE [JournalEntryLines] (
        [Id] nvarchar(450) NOT NULL,
        [JournalEntryId] nvarchar(450) NOT NULL,
        [LedgerAccountId] nvarchar(450) NOT NULL,
        [DebitAmount] float NOT NULL,
        [CreditAmount] float NOT NULL,
        [Memo] nvarchar(500) NULL,
        [LineNumber] int NOT NULL,
        [IsActive] bit NOT NULL,
        [DateCreated] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [DateModified] datetime2 NULL,
        [ModifiedBy] nvarchar(max) NULL,
        [DateStamp] nvarchar(max) NULL,
        [QId] nvarchar(max) NULL,
        CONSTRAINT [PK_JournalEntryLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_JournalEntryLines_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [JournalEntries] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_JournalEntryLines_LedgerAccounts_LedgerAccountId] FOREIGN KEY ([LedgerAccountId]) REFERENCES [LedgerAccounts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_AccountingPeriods_FiscalYear] ON [AccountingPeriods] ([FiscalYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_AccountingPeriods_QId] ON [AccountingPeriods] ([QId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AccountingPeriods_QId_FiscalYear_PeriodNumber] ON [AccountingPeriods] ([QId], [FiscalYear], [PeriodNumber]) WHERE [QId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_EntryDate] ON [JournalEntries] ([EntryDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_FiscalYear] ON [JournalEntries] ([FiscalYear]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_FiscalYear_FiscalPeriod] ON [JournalEntries] ([FiscalYear], [FiscalPeriod]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_QId] ON [JournalEntries] ([QId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_Reference] ON [JournalEntries] ([Reference]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_JournalEntries_SourceEntityType_SourceEntityId] ON [JournalEntries] ([SourceEntityType], [SourceEntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_JournalEntryLines_JournalEntryId] ON [JournalEntryLines] ([JournalEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_JournalEntryLines_LedgerAccountId] ON [JournalEntryLines] ([LedgerAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_LedgerAccounts_AccountCode] ON [LedgerAccounts] ([AccountCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_LedgerAccounts_Category] ON [LedgerAccounts] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_LedgerAccounts_ParentAccountId] ON [LedgerAccounts] ([ParentAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    CREATE INDEX [IX_LedgerAccounts_QId] ON [LedgerAccounts] ([QId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_LedgerAccounts_QId_AccountCode] ON [LedgerAccounts] ([QId], [AccountCode]) WHERE [QId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251230163444_AddAccountingModule'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251230163444_AddAccountingModule', N'9.0.1');
END;

COMMIT;
GO

