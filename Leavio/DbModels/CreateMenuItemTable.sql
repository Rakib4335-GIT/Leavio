-- Create MenuItem Table for Dynamic Menu Management
-- This table stores menu items that can be dynamically managed from the system

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[MenuItem] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [Url] NVARCHAR(500) NOT NULL,
        [Status] BIT NOT NULL DEFAULT 1,
        [Icon] NVARCHAR(500) NULL,
        [DisplayOrder] INT NOT NULL DEFAULT 0,
        [ParentId] INT NULL,
        [RequiresAuthentication] BIT NOT NULL DEFAULT 0,
        [CreatedDate] DATETIME2 NOT NULL DEFAULT GETDATE(),
        [UpdatedDate] DATETIME2 NULL,
        CONSTRAINT [PK__MenuItem__3214EC07] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Create index on Status and DisplayOrder for efficient queries
    CREATE NONCLUSTERED INDEX [IX_MenuItem_Status_DisplayOrder]
    ON [dbo].[MenuItem] ([Status] ASC, [DisplayOrder] ASC);

    -- Create index on ParentId for efficient queries
    CREATE NONCLUSTERED INDEX [IX_MenuItem_ParentId]
    ON [dbo].[MenuItem] ([ParentId] ASC);

    -- Create foreign key constraint for parent-child relationship
    ALTER TABLE [dbo].[MenuItem]
    ADD CONSTRAINT [FK_MenuItem_Parent] 
    FOREIGN KEY ([ParentId]) 
    REFERENCES [dbo].[MenuItem] ([Id])
    ON DELETE NO ACTION;

    PRINT 'MenuItem table created successfully.';
END
ELSE
BEGIN
    PRINT 'MenuItem table already exists.';
END
GO

-- Add ParentId column if it doesn't exist (for existing tables)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND name = 'ParentId')
    BEGIN
        ALTER TABLE [dbo].[MenuItem]
        ADD [ParentId] INT NULL;
        PRINT 'ParentId column added.';
    END
    ELSE
    BEGIN
        PRINT 'ParentId column already exists.';
    END
END
GO

-- Create index on ParentId if it doesn't exist
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND name = 'IX_MenuItem_ParentId')
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND name = 'ParentId')
        BEGIN
            CREATE NONCLUSTERED INDEX [IX_MenuItem_ParentId]
            ON [dbo].[MenuItem] ([ParentId] ASC);
            PRINT 'Index IX_MenuItem_ParentId created.';
        END
        ELSE
        BEGIN
            PRINT 'Cannot create index: ParentId column does not exist.';
        END
    END
    ELSE
    BEGIN
        PRINT 'Index IX_MenuItem_ParentId already exists.';
    END
END
GO

-- Drop existing foreign key constraint if it exists (to avoid conflicts)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND type in (N'U'))
BEGIN
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MenuItem_Parent]'))
    BEGIN
        ALTER TABLE [dbo].[MenuItem]
        DROP CONSTRAINT [FK_MenuItem_Parent];
        PRINT 'Existing FK_MenuItem_Parent constraint dropped.';
    END
END
GO

-- Create foreign key constraint for parent-child relationship
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_MenuItem_Parent]'))
    BEGIN
        IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND name = 'ParentId')
        BEGIN
            ALTER TABLE [dbo].[MenuItem]
            ADD CONSTRAINT [FK_MenuItem_Parent] 
            FOREIGN KEY ([ParentId]) 
            REFERENCES [dbo].[MenuItem] ([Id])
            ON DELETE NO ACTION;
            PRINT 'Foreign key constraint FK_MenuItem_Parent created.';
        END
        ELSE
        BEGIN
            PRINT 'Cannot create foreign key: ParentId column does not exist.';
        END
    END
    ELSE
    BEGIN
        PRINT 'Foreign key constraint FK_MenuItem_Parent already exists.';
    END
END
GO

-- Add RequiresAuthentication column if it doesn't exist (for existing tables)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND type in (N'U'))
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[MenuItem]') AND name = 'RequiresAuthentication')
    BEGIN
        ALTER TABLE [dbo].[MenuItem]
        ADD [RequiresAuthentication] BIT NOT NULL DEFAULT 0;
        PRINT 'RequiresAuthentication column added.';
    END
    ELSE
    BEGIN
        PRINT 'RequiresAuthentication column already exists.';
    END
END
GO

-- Insert some default menu items (optional)
IF NOT EXISTS (SELECT * FROM [dbo].[MenuItem] WHERE [Name] = 'Home')
BEGIN
    INSERT INTO [dbo].[MenuItem] ([Name], [Url], [Status], [Icon], [DisplayOrder], [ParentId], [RequiresAuthentication], [CreatedDate])
    VALUES 
        ('Home', '/', 1, NULL, 0, NULL, 0, GETDATE()),
        ('About Us', '/aboutUs', 1, NULL, 1, NULL, 0, GETDATE()),
        ('Contact', '/contact', 1, NULL, 2, NULL, 0, GETDATE());
    
    PRINT 'Default menu items inserted.';
END
ELSE
BEGIN
    PRINT 'Default menu items already exist.';
END
