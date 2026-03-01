-- ============================================================
-- MenuItem table for Menu Management (MenuManagement.razor)
-- Database: SQL Server
-- Run this script to create the table if it does not exist.
-- ============================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MenuItem] (
        [Id]                     INT IDENTITY(1,1) NOT NULL,
        [Name]                   NVARCHAR(100)    NOT NULL,
        [Url]                    NVARCHAR(500)    NOT NULL,
        [Status]                 BIT              NOT NULL DEFAULT 1,
        [Icon]                   NVARCHAR(500)    NULL,
        [DisplayOrder]           INT              NOT NULL DEFAULT 0,
        [ParentId]               INT              NULL,
        [RequiresAuthentication] BIT              NOT NULL DEFAULT 0,
        [CreatedDate]            DATETIME2(7)     NOT NULL DEFAULT GETDATE(),
        [UpdatedDate]            DATETIME2(7)     NULL,
        CONSTRAINT [PK__MenuItem__3214EC07] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_MenuItem_Status_DisplayOrder]
        ON [dbo].[MenuItem] ([Status] ASC, [DisplayOrder] ASC);

    CREATE NONCLUSTERED INDEX [IX_MenuItem_ParentId]
        ON [dbo].[MenuItem] ([ParentId] ASC);

    ALTER TABLE [dbo].[MenuItem]
        ADD CONSTRAINT [FK_MenuItem_Parent]
        FOREIGN KEY ([ParentId]) REFERENCES [dbo].[MenuItem] ([Id])
        ON DELETE NO ACTION;

    PRINT 'MenuItem table created successfully.';
END
ELSE
    PRINT 'MenuItem table already exists.';
GO

-- Remove MenuPosition column if it exists (optional: run if reverting from per-item menu position)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND type in (N'U'))
BEGIN
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND name = 'MenuPosition')
    BEGIN
        IF EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND name = 'IX_MenuItem_MenuPosition')
            DROP INDEX [IX_MenuItem_MenuPosition] ON [dbo].[MenuItem];
        ALTER TABLE [dbo].[MenuItem] DROP COLUMN [MenuPosition];
        PRINT 'MenuPosition column removed from MenuItem.';
    END
END
GO
