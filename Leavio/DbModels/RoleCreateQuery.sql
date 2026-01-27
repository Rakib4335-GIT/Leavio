-- Database Migration Script: Create Role Table and Update AdminInfo
-- This script creates a separate Role table and updates AdminInfo to use RoleId foreign key
-- Execute this script on your SQL Server database: Regester_Service

USE [Regester_Service];
GO

-- Step 1: Create Role Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Role]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Role] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RoleName] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        CONSTRAINT [PK__Role__3214EC07] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    
    PRINT 'Role table created successfully';
END
ELSE
BEGIN
    PRINT 'Role table already exists';
END
GO

-- Step 2: Add RoleId column to AdminInfo if it doesn't exist
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[AdminInfo]') 
    AND name = 'RoleId'
)
BEGIN
    -- First, drop the old Role column if it exists (from previous enum implementation)
    IF EXISTS (
        SELECT 1 
        FROM sys.columns 
        WHERE object_id = OBJECT_ID(N'[dbo].[AdminInfo]') 
        AND name = 'Role'
    )
    BEGIN
        ALTER TABLE [dbo].[AdminInfo] DROP COLUMN [Role];
        PRINT 'Old Role column (enum) removed';
    END

    -- Add RoleId column
    ALTER TABLE [dbo].[AdminInfo]
    ADD [RoleId] INT NOT NULL DEFAULT 1;
    
    PRINT 'RoleId column added to AdminInfo table';
END
ELSE
BEGIN
    PRINT 'RoleId column already exists in AdminInfo table';
END
GO

-- Step 3: Create Foreign Key Constraint
IF NOT EXISTS (
    SELECT 1 
    FROM sys.foreign_keys 
    WHERE name = 'FK_AdminInfo_Role'
)
BEGIN
    ALTER TABLE [dbo].[AdminInfo]
    ADD CONSTRAINT [FK_AdminInfo_Role] 
    FOREIGN KEY ([RoleId]) 
    REFERENCES [dbo].[Role] ([Id])
    ON DELETE NO ACTION
    ON UPDATE NO ACTION;
    
    PRINT 'Foreign key constraint FK_AdminInfo_Role created';
END
ELSE
BEGIN
    PRINT 'Foreign key constraint FK_AdminInfo_Role already exists';
END
GO

-- Step 4: Insert default roles (User and Admin)
IF NOT EXISTS (SELECT 1 FROM [dbo].[Role] WHERE [RoleName] = 'User')
BEGIN
    INSERT INTO [dbo].[Role] ([RoleName], [Description])
    VALUES ('User', 'Standard user role with basic permissions');
    PRINT 'Default User role inserted';
END
ELSE
BEGIN
    PRINT 'User role already exists';
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Role] WHERE [RoleName] = 'Admin')
BEGIN
    INSERT INTO [dbo].[Role] ([RoleName], [Description])
    VALUES ('Admin', 'Administrator role with full system access');
    PRINT 'Default Admin role inserted';
END
ELSE
BEGIN
    PRINT 'Admin role already exists';
END
GO

-- Step 5: Update existing AdminInfo records to have RoleId = 1 (User) if they don't have a valid RoleId
UPDATE [dbo].[AdminInfo]
SET [RoleId] = 1
WHERE [RoleId] NOT IN (SELECT [Id] FROM [dbo].[Role])
   OR [RoleId] IS NULL;
GO

-- Step 6: Verify the setup
SELECT 
    r.Id,
    r.RoleName,
    r.Description,
    COUNT(a.Id) AS UserCount
FROM [dbo].[Role] r
LEFT JOIN [dbo].[AdminInfo] a ON r.Id = a.RoleId
GROUP BY r.Id, r.RoleName, r.Description
ORDER BY r.Id;
GO

PRINT 'Migration completed successfully!';
PRINT 'You can now manage roles through the Role Management page or directly in the database.';
GO
