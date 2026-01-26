-- Database Migration Script: Add Role Column to AdminInfo Table
-- This script adds a Role column to the AdminInfo table to support Admin and User roles
-- Execute this script on your SQL Server database: Regester_Service

USE [Regester_Service];
GO

-- Check if Role column already exists
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[AdminInfo]') 
    AND name = 'Role'
)
BEGIN
    -- Add Role column with default value of 1 (User)
    ALTER TABLE [dbo].[AdminInfo]
    ADD [Role] INT NOT NULL DEFAULT 1;
    
    PRINT 'Role column added successfully with default value of 1 (User)';
END
ELSE
BEGIN
    PRINT 'Role column already exists in AdminInfo table';
END
GO

-- Update existing records to have User role (1) if they are NULL
UPDATE [dbo].[AdminInfo]
SET [Role] = 1
WHERE [Role] IS NULL;
GO

-- Verify the column was added
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AdminInfo' 
AND COLUMN_NAME = 'Role';
GO

PRINT 'Migration completed successfully!';
PRINT 'Role values: 1 = User, 2 = Admin';
GO
