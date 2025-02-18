﻿IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230330025437_mssql.onprem_migration_921'
)
BEGIN
    CREATE TABLE [Foods] (
        [FoodId] uniqueidentifier NOT NULL,
        [FriendlyCode] varchar(64) NOT NULL,
        [Cite] nvarchar(max) NULL,
        [Details] nvarchar(max) NULL,
        [FriendlyName] varchar(64) NOT NULL,
        CONSTRAINT [PK_Foods] PRIMARY KEY ([FoodId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230330025437_mssql.onprem_migration_921'
)
BEGIN
    CREATE TABLE [Nutrients] (
        [NutrientId] int NOT NULL IDENTITY,
        [DefaultMeasureUnit] varchar(64) NOT NULL,
        [Details] nvarchar(max) NULL,
        [FriendlyName] varchar(64) NOT NULL,
        CONSTRAINT [PK_Nutrients] PRIMARY KEY ([NutrientId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230330025437_mssql.onprem_migration_921'
)
BEGIN
    CREATE TABLE [FoodNutrientValues] (
        [FoodNutrientValueId] int NOT NULL IDENTITY,
        [Value] decimal(18,2) NOT NULL,
        [MeasureUnit] varchar(64) NULL,
        [Details] nvarchar(max) NULL,
        [FoodId] uniqueidentifier NOT NULL,
        [NutrientId] int NOT NULL,
        CONSTRAINT [PK_FoodNutrientValues] PRIMARY KEY ([FoodNutrientValueId]),
        CONSTRAINT [FK_FoodNutrientValues_Foods_FoodId] FOREIGN KEY ([FoodId]) REFERENCES [Foods] ([FoodId]) ON DELETE CASCADE,
        CONSTRAINT [FK_FoodNutrientValues_Nutrients_NutrientId] FOREIGN KEY ([NutrientId]) REFERENCES [Nutrients] ([NutrientId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230330025437_mssql.onprem_migration_921'
)
BEGIN
    CREATE INDEX [IX_FoodNutrientValues_FoodId] ON [FoodNutrientValues] ([FoodId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230330025437_mssql.onprem_migration_921'
)
BEGIN
    CREATE INDEX [IX_FoodNutrientValues_NutrientId] ON [FoodNutrientValues] ([NutrientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230330025437_mssql.onprem_migration_921'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230330025437_mssql.onprem_migration_921', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331062257_mssql.onprem_migration_578'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230331062257_mssql.onprem_migration_578', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331062441_mssql.onprem_migration_348'
)
BEGIN
    CREATE TABLE [People] (
        [PersonId] uniqueidentifier NOT NULL,
        [AgeStart] decimal(18,2) NULL,
        [AgeEnd] decimal(18,2) NULL,
        [Gender] nvarchar(max) NULL,
        [SpecialPhysiologicalPeriod] nvarchar(max) NULL,
        [Illness] nvarchar(max) NULL,
        [Cite] nvarchar(max) NULL,
        [BodySize] nvarchar(max) NULL,
        [PhysicalActivityLevel] nvarchar(max) NULL,
        [DerivedFromPersonId] uniqueidentifier NULL,
        [Details] nvarchar(max) NULL,
        [FriendlyName] varchar(64) NOT NULL,
        CONSTRAINT [PK_People] PRIMARY KEY ([PersonId]),
        CONSTRAINT [FK_People_People_DerivedFromPersonId] FOREIGN KEY ([DerivedFromPersonId]) REFERENCES [People] ([PersonId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331062441_mssql.onprem_migration_348'
)
BEGIN
    CREATE INDEX [IX_People_DerivedFromPersonId] ON [People] ([DerivedFromPersonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331062441_mssql.onprem_migration_348'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230331062441_mssql.onprem_migration_348', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331144722_mssql.onprem_migration_569'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230331144722_mssql.onprem_migration_569', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331152300_mssql.onprem_migration_460'
)
BEGIN
    CREATE TABLE [MultiDerivedPersonRelationships] (
        [MultiDerivedPersonRelationshipId] uniqueidentifier NOT NULL,
        [ChildModelId] uniqueidentifier NULL,
        [ParentModelId] uniqueidentifier NULL,
        CONSTRAINT [PK_MultiDerivedPersonRelationships] PRIMARY KEY ([MultiDerivedPersonRelationshipId]),
        CONSTRAINT [FK_MultiDerivedPersonRelationships_People_ChildModelId] FOREIGN KEY ([ChildModelId]) REFERENCES [People] ([PersonId]),
        CONSTRAINT [FK_MultiDerivedPersonRelationships_People_ParentModelId] FOREIGN KEY ([ParentModelId]) REFERENCES [People] ([PersonId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331152300_mssql.onprem_migration_460'
)
BEGIN
    CREATE INDEX [IX_MultiDerivedPersonRelationships_ChildModelId] ON [MultiDerivedPersonRelationships] ([ChildModelId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331152300_mssql.onprem_migration_460'
)
BEGIN
    CREATE INDEX [IX_MultiDerivedPersonRelationships_ParentModelId] ON [MultiDerivedPersonRelationships] ([ParentModelId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230331152300_mssql.onprem_migration_460'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230331152300_mssql.onprem_migration_460', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230410143020_mssql.onprem_migration_124'
)
BEGIN
    CREATE TABLE [PersonalDietaryReferenceIntakes] (
        [PersonalDietaryReferenceIntakeValueId] int NOT NULL IDENTITY,
        [PersonId] uniqueidentifier NOT NULL,
        [NutrientId] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [MeasureUnit] varchar(64) NULL,
        CONSTRAINT [PK_PersonalDietaryReferenceIntakes] PRIMARY KEY ([PersonalDietaryReferenceIntakeValueId]),
        CONSTRAINT [FK_PersonalDietaryReferenceIntakes_Nutrients_NutrientId] FOREIGN KEY ([NutrientId]) REFERENCES [Nutrients] ([NutrientId]) ON DELETE CASCADE,
        CONSTRAINT [FK_PersonalDietaryReferenceIntakes_People_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [People] ([PersonId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230410143020_mssql.onprem_migration_124'
)
BEGIN
    CREATE INDEX [IX_PersonalDietaryReferenceIntakes_NutrientId] ON [PersonalDietaryReferenceIntakes] ([NutrientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230410143020_mssql.onprem_migration_124'
)
BEGIN
    CREATE INDEX [IX_PersonalDietaryReferenceIntakes_PersonId] ON [PersonalDietaryReferenceIntakes] ([PersonId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230410143020_mssql.onprem_migration_124'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230410143020_mssql.onprem_migration_124', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    ALTER TABLE [PersonalDietaryReferenceIntakes] ADD [Details] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    ALTER TABLE [PersonalDietaryReferenceIntakes] ADD [IsOffsetValue] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    ALTER TABLE [PersonalDietaryReferenceIntakes] ADD [ReferenceType] nvarchar(64) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    CREATE TABLE [Advices] (
        [AdviceId] int NOT NULL IDENTITY,
        [Content] nvarchar(max) NULL,
        [Priority] int NOT NULL,
        CONSTRAINT [PK_Advices] PRIMARY KEY ([AdviceId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    CREATE TABLE [Diseases] (
        [DiseaseId] int NOT NULL IDENTITY,
        [FriendlyName] nvarchar(max) NULL,
        [ICD10] nvarchar(max) NULL,
        CONSTRAINT [PK_Diseases] PRIMARY KEY ([DiseaseId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    CREATE TABLE [EERs] (
        [EERId] int NOT NULL IDENTITY,
        [Gender] nvarchar(max) NULL,
        [AgeStart] decimal(18,2) NULL,
        [PAL] decimal(18,2) NULL,
        [AvgBwEER] int NOT NULL,
        [BEE] decimal(18,2) NULL,
        CONSTRAINT [PK_EERs] PRIMARY KEY ([EERId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    CREATE TABLE [AdviceDisease] (
        [AdvicesAdviceId] int NOT NULL,
        [DiseasesDiseaseId] int NOT NULL,
        CONSTRAINT [PK_AdviceDisease] PRIMARY KEY ([AdvicesAdviceId], [DiseasesDiseaseId]),
        CONSTRAINT [FK_AdviceDisease_Advices_AdvicesAdviceId] FOREIGN KEY ([AdvicesAdviceId]) REFERENCES [Advices] ([AdviceId]) ON DELETE CASCADE,
        CONSTRAINT [FK_AdviceDisease_Diseases_DiseasesDiseaseId] FOREIGN KEY ([DiseasesDiseaseId]) REFERENCES [Diseases] ([DiseaseId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    CREATE INDEX [IX_AdviceDisease_DiseasesDiseaseId] ON [AdviceDisease] ([DiseasesDiseaseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124248_mssql.onprem_migration_695'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230731124248_mssql.onprem_migration_695', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230731124327_mssql.onprem_migration_968'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230731124327_mssql.onprem_migration_968', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230806141846_mssql.onprem_migration_313'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EERs]') AND [c].[name] = N'AvgBwEER');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [EERs] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [EERs] ALTER COLUMN [AvgBwEER] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20230806141846_mssql.onprem_migration_313'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20230806141846_mssql.onprem_migration_313', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    ALTER TABLE [FoodNutrientValues] DROP CONSTRAINT [FK_FoodNutrientValues_Nutrients_NutrientId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    DROP TABLE [MultiDerivedPersonRelationships];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    DROP TABLE [PersonalDietaryReferenceIntakes];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    DROP TABLE [Nutrients];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    DROP TABLE [People];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    CREATE TABLE [DRIs] (
        [DietaryReferenceIntakeValueId] int NOT NULL IDENTITY,
        [AgeStart] decimal(18,2) NULL,
        [Gender] nvarchar(max) NULL,
        [SpecialPhysiologicalPeriod] nvarchar(max) NULL,
        [Nutrient] varchar(64) NOT NULL,
        [RecordType] int NOT NULL,
        [IsOffset] bit NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [MeasureUnit] varchar(64) NOT NULL,
        [Detail] nvarchar(max) NULL,
        CONSTRAINT [PK_DRIs] PRIMARY KEY ([DietaryReferenceIntakeValueId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    CREATE TABLE [Nutrient] (
        [NutrientId] int NOT NULL IDENTITY,
        [DefaultMeasureUnit] varchar(64) NOT NULL,
        [Details] nvarchar(max) NULL,
        [FriendlyName] varchar(64) NOT NULL,
        CONSTRAINT [PK_Nutrient] PRIMARY KEY ([NutrientId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    ALTER TABLE [FoodNutrientValues] ADD CONSTRAINT [FK_FoodNutrientValues_Nutrient_NutrientId] FOREIGN KEY ([NutrientId]) REFERENCES [Nutrient] ([NutrientId]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231031125309_mssql.onprem_migration_788'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20231031125309_mssql.onprem_migration_788', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231113111131_mssql.onprem_migration_362'
)
BEGIN
    ALTER TABLE [EERs] ADD [OffsetEnergy] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231113111131_mssql.onprem_migration_362'
)
BEGIN
    ALTER TABLE [EERs] ADD [SpecialPhysiologicalPeriod] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20231113111131_mssql.onprem_migration_362'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20231113111131_mssql.onprem_migration_362', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250208015529_mssql.onprem_migration_128'
)
BEGIN
    ALTER TABLE [Foods] ADD [EdiblePortion] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250208015529_mssql.onprem_migration_128'
)
BEGIN
    ALTER TABLE [Foods] ADD [FoodGroups] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250208015529_mssql.onprem_migration_128'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250208015529_mssql.onprem_migration_128', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250208032244_mssql.onprem_migration_122'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Nutrient]') AND [c].[name] = N'FriendlyName');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Nutrient] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Nutrient] ALTER COLUMN [FriendlyName] nvarchar(64) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250208032244_mssql.onprem_migration_122'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Foods]') AND [c].[name] = N'FriendlyName');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Foods] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Foods] ALTER COLUMN [FriendlyName] nvarchar(64) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250208032244_mssql.onprem_migration_122'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250208032244_mssql.onprem_migration_122', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250208055927_mssql.onprem_migration_144'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250208055927_mssql.onprem_migration_144', N'8.0.8');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250209030037_mssql.onprem_migration_277'
)
BEGIN
    ALTER TABLE [FoodNutrientValues] DROP CONSTRAINT [FK_FoodNutrientValues_Nutrient_NutrientId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250209030037_mssql.onprem_migration_277'
)
BEGIN
    ALTER TABLE [Nutrient] DROP CONSTRAINT [PK_Nutrient];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250209030037_mssql.onprem_migration_277'
)
BEGIN
    EXEC sp_rename N'[Nutrient]', N'Nutrients';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250209030037_mssql.onprem_migration_277'
)
BEGIN
    ALTER TABLE [Nutrients] ADD CONSTRAINT [PK_Nutrients] PRIMARY KEY ([NutrientId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250209030037_mssql.onprem_migration_277'
)
BEGIN
    ALTER TABLE [FoodNutrientValues] ADD CONSTRAINT [FK_FoodNutrientValues_Nutrients_NutrientId] FOREIGN KEY ([NutrientId]) REFERENCES [Nutrients] ([NutrientId]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250209030037_mssql.onprem_migration_277'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250209030037_mssql.onprem_migration_277', N'8.0.8');
END;
GO

COMMIT;
GO

