-- Database Migration Script: Create User_Role Table
-- This script creates a User_Role junction table to track user-role assignments
-- Execute this script on your SQL Server database: Regester_Service

USE [Regester_Service];
GO

-- Step 1: Create User_Role Table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[User_Role]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[User_Role] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [UserId] INT NOT NULL,
        [RoleId] INT NOT NULL,
        CONSTRAINT [PK__UserRole__3214EC07] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    
    PRINT 'User_Role table created successfully';
END
ELSE
BEGIN
    PRINT 'User_Role table already exists';
END
GO

-- Step 2: Create Foreign Key to AdminInfo (User)
IF NOT EXISTS (
    SELECT 1 
    FROM sys.foreign_keys 
    WHERE name = 'FK_UserRole_AdminInfo'
)
BEGIN
    ALTER TABLE [dbo].[User_Role]
    ADD CONSTRAINT [FK_UserRole_AdminInfo] 
    FOREIGN KEY ([UserId]) 
    REFERENCES [dbo].[AdminInfo] ([Id])
    ON DELETE CASCADE
    ON UPDATE NO ACTION;
    
    PRINT 'Foreign key constraint FK_UserRole_AdminInfo created';
END
ELSE
BEGIN
    PRINT 'Foreign key constraint FK_UserRole_AdminInfo already exists';
END
GO

-- Step 3: Create Foreign Key to Role
IF NOT EXISTS (
    SELECT 1 
    FROM sys.foreign_keys 
    WHERE name = 'FK_UserRole_Role'
)
BEGIN
    ALTER TABLE [dbo].[User_Role]
    ADD CONSTRAINT [FK_UserRole_Role] 
    FOREIGN KEY ([RoleId]) 
    REFERENCES [dbo].[Role] ([Id])
    ON DELETE NO ACTION
    ON UPDATE NO ACTION;
    
    PRINT 'Foreign key constraint FK_UserRole_Role created';
END
ELSE
BEGIN
    PRINT 'Foreign key constraint FK_UserRole_Role already exists';
END
GO

-- Step 4: Create Unique Index to prevent duplicate assignments
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_UserRole_UserId_RoleId' 
    AND object_id = OBJECT_ID(N'[dbo].[User_Role]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_UserRole_UserId_RoleId]
    ON [dbo].[User_Role] ([UserId], [RoleId]);
    
    PRINT 'Unique index IX_UserRole_UserId_RoleId created';
END
ELSE
BEGIN
    PRINT 'Unique index IX_UserRole_UserId_RoleId already exists';
END
GO

-- Step 5: Migrate existing data from AdminInfo.RoleId to User_Role table
-- This will create User_Role entries for all existing users based on their RoleId
IF NOT EXISTS (SELECT 1 FROM [dbo].[User_Role])
BEGIN
    INSERT INTO [dbo].[User_Role] ([UserId], [RoleId])
    SELECT [Id] AS [UserId], [RoleId]
    FROM [dbo].[AdminInfo]
    WHERE [RoleId] IS NOT NULL
      AND [RoleId] IN (SELECT [Id] FROM [dbo].[Role]);
    
    DECLARE @RowCount INT = @@ROWCOUNT;
    PRINT CONCAT('Migrated ', @RowCount, ' existing user-role assignments to User_Role table');
END
ELSE
BEGIN
    PRINT 'User_Role table already contains data. Skipping migration.';
END
GO

-- Step 6: Verify the setup
SELECT 
    ur.Id AS UserRoleId,
    ur.UserId,
    u.Name AS UserName,
    u.Email AS UserEmail,
    ur.RoleId,
    r.RoleName
FROM [dbo].[User_Role] ur
INNER JOIN [dbo].[AdminInfo] u ON ur.UserId = u.Id
INNER JOIN [dbo].[Role] r ON ur.RoleId = r.Id
ORDER BY u.Name, r.RoleName;
GO

PRINT 'Migration completed successfully!';
PRINT 'You can now manage user-role assignments through the User Role Management page.';
GO
