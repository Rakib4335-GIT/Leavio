-- Remove RoleId column from AdminInfo table
-- This script removes the RoleId column since we're using User_Role table exclusively

USE [Regester_Service];
GO

PRINT '========================================';
PRINT 'Removing RoleId from AdminInfo table';
PRINT '========================================';
GO

-- Step 1: Drop the foreign key constraint if it exists
IF EXISTS (
    SELECT 1 
    FROM sys.foreign_keys 
    WHERE name = 'FK_AdminInfo_Role'
)
BEGIN
    PRINT '';
    PRINT 'Dropping FK_AdminInfo_Role constraint...';
    ALTER TABLE [dbo].[AdminInfo]
    DROP CONSTRAINT [FK_AdminInfo_Role];
    PRINT 'Constraint dropped successfully';
END
ELSE
BEGIN
    PRINT '';
    PRINT 'FK_AdminInfo_Role constraint does not exist';
END
GO

-- Step 2: Remove RoleId column
IF EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE object_id = OBJECT_ID(N'[dbo].[AdminInfo]') 
    AND name = 'RoleId'
)
BEGIN
    PRINT '';
    PRINT 'Removing RoleId column from AdminInfo table...';
    ALTER TABLE [dbo].[AdminInfo]
    DROP COLUMN [RoleId];
    PRINT 'RoleId column removed successfully';
END
ELSE
BEGIN
    PRINT '';
    PRINT 'RoleId column does not exist in AdminInfo table';
END
GO

-- Step 3: Verify the change
PRINT '';
PRINT '========================================';
PRINT 'Verification - AdminInfo table structure:';
PRINT '========================================';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AdminInfo'
ORDER BY ORDINAL_POSITION;
GO

PRINT '';
PRINT '========================================';
PRINT 'Removal completed successfully!';
PRINT '========================================';
PRINT 'RoleId column has been removed from AdminInfo.';
PRINT 'All role information is now stored in User_Role table.';
GO
