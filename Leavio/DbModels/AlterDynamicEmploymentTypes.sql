-- Dynamic employment types + per–leave-type allocations (run after backup).
-- Migrates legacy AdminInfo.EmploymentType (string) and LeaveType FT/Intern columns into normalized tables.

IF OBJECT_ID('dbo.EmploymentType', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmploymentType
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Code NVARCHAR(50) NOT NULL,
        DisplayOrder INT NOT NULL CONSTRAINT DF_EmploymentType_DisplayOrder DEFAULT(0),
        IsActive BIT NOT NULL CONSTRAINT DF_EmploymentType_IsActive DEFAULT(1),
        CONSTRAINT UQ_EmploymentType_Name UNIQUE (Name),
        CONSTRAINT UQ_EmploymentType_Code UNIQUE (Code)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.EmploymentType WHERE Code = N'FullTime')
    INSERT INTO dbo.EmploymentType (Name, Code, DisplayOrder, IsActive) VALUES (N'Full-Time', N'FullTime', 1, 1);
IF NOT EXISTS (SELECT 1 FROM dbo.EmploymentType WHERE Code = N'Intern')
    INSERT INTO dbo.EmploymentType (Name, Code, DisplayOrder, IsActive) VALUES (N'Intern', N'Intern', 2, 1);
GO

IF OBJECT_ID('dbo.LeaveTypeEmploymentAllocation', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveTypeEmploymentAllocation
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        LeaveTypeId INT NOT NULL,
        EmploymentTypeId INT NOT NULL,
        AllocationDays DECIMAL(5,2) NOT NULL CONSTRAINT DF_LeaveTypeEmploymentAllocation_AllocationDays DEFAULT(0),
        IsAvailable BIT NOT NULL CONSTRAINT DF_LeaveTypeEmploymentAllocation_IsAvailable DEFAULT(1),
        CONSTRAINT FK_LeaveTypeEmploymentAllocation_LeaveType FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveType(Id) ON DELETE CASCADE,
        CONSTRAINT FK_LeaveTypeEmploymentAllocation_EmploymentType FOREIGN KEY (EmploymentTypeId) REFERENCES dbo.EmploymentType(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_LeaveType_EmploymentType UNIQUE (LeaveTypeId, EmploymentTypeId)
    );
END;
GO

-- Populate allocations from legacy LeaveType columns (if present)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.LeaveType') AND name = 'FullTimeAllocationDays')
BEGIN
    DECLARE @FT INT = (SELECT TOP 1 Id FROM dbo.EmploymentType WHERE Code = N'FullTime');
    DECLARE @IN INT = (SELECT TOP 1 Id FROM dbo.EmploymentType WHERE Code = N'Intern');

    INSERT INTO dbo.LeaveTypeEmploymentAllocation (LeaveTypeId, EmploymentTypeId, AllocationDays, IsAvailable)
    SELECT lt.Id, @FT,
           CASE WHEN lt.FullTimeAllocationDays > 0 THEN lt.FullTimeAllocationDays ELSE lt.DefaultAllocationDays END,
           CASE WHEN lt.AvailableForFullTime = 1 AND (CASE WHEN lt.FullTimeAllocationDays > 0 THEN lt.FullTimeAllocationDays ELSE lt.DefaultAllocationDays END) > 0 THEN 1 ELSE 0 END
    FROM dbo.LeaveType lt
    WHERE NOT EXISTS (SELECT 1 FROM dbo.LeaveTypeEmploymentAllocation x WHERE x.LeaveTypeId = lt.Id AND x.EmploymentTypeId = @FT);

    INSERT INTO dbo.LeaveTypeEmploymentAllocation (LeaveTypeId, EmploymentTypeId, AllocationDays, IsAvailable)
    SELECT lt.Id, @IN, lt.InternAllocationDays,
           CASE WHEN lt.AvailableForIntern = 1 AND lt.InternAllocationDays > 0 THEN 1 ELSE 0 END
    FROM dbo.LeaveType lt
    WHERE NOT EXISTS (SELECT 1 FROM dbo.LeaveTypeEmploymentAllocation x WHERE x.LeaveTypeId = lt.Id AND x.EmploymentTypeId = @IN);
END;
GO

-- AdminInfo: EmploymentTypeId
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdminInfo') AND name = 'EmploymentTypeId')
BEGIN
    ALTER TABLE dbo.AdminInfo ADD EmploymentTypeId INT NULL;
END;
GO

DECLARE @FTId INT = (SELECT TOP 1 Id FROM dbo.EmploymentType WHERE Code = N'FullTime');
DECLARE @INId INT = (SELECT TOP 1 Id FROM dbo.EmploymentType WHERE Code = N'Intern');

UPDATE dbo.AdminInfo SET EmploymentTypeId = @FTId WHERE EmploymentTypeId IS NULL AND (@FTId IS NOT NULL);
UPDATE dbo.AdminInfo SET EmploymentTypeId = @INId WHERE EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdminInfo') AND name = 'EmploymentType')
    AND (EmploymentType = N'Intern' OR EmploymentType LIKE N'Intern%') AND @INId IS NOT NULL;

UPDATE dbo.AdminInfo SET EmploymentTypeId = @FTId WHERE EmploymentTypeId IS NULL AND @FTId IS NOT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdminInfo') AND name = 'EmploymentTypeId')
   AND NOT EXISTS (SELECT 1 FROM dbo.AdminInfo WHERE EmploymentTypeId IS NULL)
BEGIN
    ALTER TABLE dbo.AdminInfo ALTER COLUMN EmploymentTypeId INT NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AdminInfo_EmploymentType')
BEGIN
    ALTER TABLE dbo.AdminInfo WITH NOCHECK
    ADD CONSTRAINT FK_AdminInfo_EmploymentType FOREIGN KEY (EmploymentTypeId) REFERENCES dbo.EmploymentType(Id);
END;
GO

-- Optional: drop legacy columns after verifying the app
-- ALTER TABLE dbo.AdminInfo DROP CONSTRAINT DF_AdminInfo_EmploymentType; ALTER TABLE dbo.AdminInfo DROP COLUMN EmploymentType;
-- ALTER TABLE dbo.LeaveType DROP CONSTRAINT DF_LeaveType_AvailableForFullTime; ... (drop FT/Intern columns)
