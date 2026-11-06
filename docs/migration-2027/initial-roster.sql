BEGIN TRANSACTION;
CREATE TABLE [people].[RosterBatches] (
    [Id] uniqueidentifier NOT NULL,
    [FileSha256] varchar(64) NOT NULL,
    [PlanSha256] varchar(64) NOT NULL,
    [AcademicYearId] uniqueidentifier NOT NULL,
    [Source] varchar(40) NOT NULL,
    [ActorId] uniqueidentifier NOT NULL,
    [AppliedAt] datetimeoffset NOT NULL,
    [ResultJson] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_RosterBatches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RosterBatches_AspNetUsers_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [iam].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RosterBatches_Years_AcademicYearId] FOREIGN KEY ([AcademicYearId]) REFERENCES [academic].[Years] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [people].[RosterSourceAliases] (
    [Source] varchar(40) NOT NULL,
    [Entity] varchar(20) NOT NULL,
    [LegacyId] varchar(60) NOT NULL,
    [Discriminator] varchar(40) NOT NULL,
    [PersonId] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_RosterSourceAliases] PRIMARY KEY ([Source], [Entity], [LegacyId], [Discriminator]),
    CONSTRAINT [FK_RosterSourceAliases_Persons_PersonId] FOREIGN KEY ([PersonId]) REFERENCES [people].[Persons] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_RosterSourceAliases_RosterBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [people].[RosterBatches] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_RosterBatches_AcademicYearId] ON [people].[RosterBatches] ([AcademicYearId]);

CREATE INDEX [IX_RosterBatches_ActorId] ON [people].[RosterBatches] ([ActorId]);

CREATE INDEX [IX_RosterSourceAliases_BatchId] ON [people].[RosterSourceAliases] ([BatchId]);

CREATE INDEX [IX_RosterSourceAliases_PersonId] ON [people].[RosterSourceAliases] ([PersonId]);

INSERT INTO [school].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261106202522_InitialRosterImport', N'10.0.14');

COMMIT;
GO
