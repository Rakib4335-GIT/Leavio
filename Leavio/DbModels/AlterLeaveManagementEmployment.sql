-- One-time migration: employment-based leave allocation + employee employment type.
-- Run against your Leavio database after backing up. Safe to run once; re-running ALTERs is idempotent.

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

-- Backfill full-time days from legacy column
UPDATE dbo.LeaveType
SET FullTimeAllocationDays = DefaultAllocationDays
WHERE FullTimeAllocationDays = 0 AND DefaultAllocationDays > 0;
GO

-- Optional: mirror defaults for interns (edit later in Leave Management if needed)
UPDATE dbo.LeaveType
SET InternAllocationDays = DefaultAllocationDays,
    AvailableForIntern = 1
WHERE InternAllocationDays = 0 AND AvailableForIntern = 0 AND DefaultAllocationDays > 0;
GO
