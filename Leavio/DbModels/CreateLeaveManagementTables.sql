-- Leave Management System Tables
-- Run this script on SQL Server database: Leavio

IF OBJECT_ID('dbo.LeaveType', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveType
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL UNIQUE,
        DefaultAllocationDays DECIMAL(5,2) NOT NULL CONSTRAINT DF_LeaveType_DefaultAllocationDays DEFAULT(0),
        RequiresDocumentForMultiDay BIT NOT NULL CONSTRAINT DF_LeaveType_RequiresDocumentForMultiDay DEFAULT(0),
        IsActive BIT NOT NULL CONSTRAINT DF_LeaveType_IsActive DEFAULT(1),
        DisplayOrder INT NOT NULL CONSTRAINT DF_LeaveType_DisplayOrder DEFAULT(0)
    );
END;
GO

IF OBJECT_ID('dbo.LeaveBalance', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveBalance
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeId INT NOT NULL,
        LeaveTypeId INT NOT NULL,
        TotalAllocatedDays DECIMAL(5,2) NOT NULL,
        UsedDays DECIMAL(5,2) NOT NULL CONSTRAINT DF_LeaveBalance_UsedDays DEFAULT(0),
        ValidFrom DATE NOT NULL,
        ValidTo DATE NOT NULL,
        UpdatedOn DATETIME2 NOT NULL CONSTRAINT DF_LeaveBalance_UpdatedOn DEFAULT(SYSDATETIME()),
        CONSTRAINT FK_LeaveBalance_AdminInfo FOREIGN KEY (EmployeeId) REFERENCES dbo.AdminInfo(Id) ON DELETE CASCADE,
        CONSTRAINT FK_LeaveBalance_LeaveType FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveType(Id),
        CONSTRAINT UQ_LeaveBalance_Employee_LeaveType_Period UNIQUE (EmployeeId, LeaveTypeId, ValidFrom, ValidTo)
    );
END;
GO

IF OBJECT_ID('dbo.LeaveApplication', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LeaveApplication
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeId INT NOT NULL,
        LeaveTypeId INT NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        DurationDays DECIMAL(5,2) NOT NULL,
        Reason NVARCHAR(2000) NOT NULL,
        SupportingDocumentPath NVARCHAR(500) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_LeaveApplication_Status DEFAULT('Pending'),
        AppliedOn DATETIME2 NOT NULL CONSTRAINT DF_LeaveApplication_AppliedOn DEFAULT(SYSDATETIME()),
        ReviewedByEmployeeId INT NULL,
        ReviewedOn DATETIME2 NULL,
        ReviewerComments NVARCHAR(1000) NULL,
        CONSTRAINT CK_LeaveApplication_Status CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
        CONSTRAINT CK_LeaveApplication_Dates CHECK (StartDate <= EndDate),
        CONSTRAINT FK_LeaveApplication_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.AdminInfo(Id) ON DELETE CASCADE,
        CONSTRAINT FK_LeaveApplication_LeaveType FOREIGN KEY (LeaveTypeId) REFERENCES dbo.LeaveType(Id),
        CONSTRAINT FK_LeaveApplication_ReviewedBy FOREIGN KEY (ReviewedByEmployeeId) REFERENCES dbo.AdminInfo(Id)
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LeaveApplication_Employee_Status' AND object_id = OBJECT_ID('dbo.LeaveApplication'))
BEGIN
    CREATE INDEX IX_LeaveApplication_Employee_Status ON dbo.LeaveApplication(EmployeeId, Status);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.LeaveType WHERE Name = 'Sick Leave')
BEGIN
    INSERT INTO dbo.LeaveType (Name, DefaultAllocationDays, RequiresDocumentForMultiDay, IsActive, DisplayOrder)
    VALUES ('Sick Leave', 10, 1, 1, 1);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.LeaveType WHERE Name = 'Annual Leave')
BEGIN
    INSERT INTO dbo.LeaveType (Name, DefaultAllocationDays, RequiresDocumentForMultiDay, IsActive, DisplayOrder)
    VALUES ('Annual Leave', 14, 0, 1, 2);
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.LeaveType WHERE Name = 'Casual Leave')
BEGIN
    INSERT INTO dbo.LeaveType (Name, DefaultAllocationDays, RequiresDocumentForMultiDay, IsActive, DisplayOrder)
    VALUES ('Casual Leave', 10, 0, 1, 3);
END;
GO

-- Employment-based allocation (run on existing DBs; idempotent)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AdminInfo') AND name = 'EmploymentType')
BEGIN
    ALTER TABLE dbo.AdminInfo ADD EmploymentType NVARCHAR(20) NOT NULL CONSTRAINT DF_AdminInfo_EmploymentType DEFAULT('FullTime');
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.LeaveType') AND name = 'AvailableForFullTime')
BEGIN
    ALTER TABLE dbo.LeaveType ADD AvailableForFullTime BIT NOT NULL CONSTRAINT DF_LeaveType_AvailableForFullTime DEFAULT(1);
    ALTER TABLE dbo.LeaveType ADD FullTimeAllocationDays DECIMAL(5,2) NOT NULL CONSTRAINT DF_LeaveType_FullTimeAllocationDays DEFAULT(0);
    ALTER TABLE dbo.LeaveType ADD AvailableForIntern BIT NOT NULL CONSTRAINT DF_LeaveType_AvailableForIntern DEFAULT(0);
    ALTER TABLE dbo.LeaveType ADD InternAllocationDays DECIMAL(5,2) NOT NULL CONSTRAINT DF_LeaveType_InternAllocationDays DEFAULT(0);
END;
GO

UPDATE dbo.LeaveType SET FullTimeAllocationDays = DefaultAllocationDays WHERE FullTimeAllocationDays = 0 AND DefaultAllocationDays > 0;
GO

-- For dynamic employment types + per-type allocations + AdminInfo.EmploymentTypeId, run:
-- DbModels/AlterDynamicEmploymentTypes.sql
GO
