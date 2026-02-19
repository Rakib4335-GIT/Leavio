# Daily Login Tracking System - Implementation Guide

## Overview
This document describes the daily login tracking system implemented for the Leavio application. The system tracks the first login time and last logout time for each user (identified by EmployeeId) on a daily basis.

## Database Design

### Table: DailyLoginTracking
- **Id**: INT (Primary Key, Identity)
- **EmployeeId**: NVARCHAR(100) (Required) - Identifies the user
- **FirstLoginTime**: DATETIME2 (Required) - First login time of the day
- **LastLogoutTime**: DATETIME2 (Nullable) - Last logout time of the day
- **TrackingDate**: DATE (Required) - The date being tracked

### Index
- Composite index on (EmployeeId, TrackingDate) for efficient queries

## Implementation Details

### 1. Database Model
**File**: `DbModels/DailyLoginTracking.cs`
- Entity class representing the DailyLoginTracking table

### 2. Database Context
**File**: `DbModels/RegesterServiceContext.cs`
- Added `DbSet<DailyLoginTracking>` property
- Configured entity mapping with proper indexes

### 3. SQL Scripts
**Files**: 
- `DbModels/CreateDailyLoginTrackingTable.sql` - Creates the table
- `DbModels/LoginTrackingQueries.sql` - Sample queries for retrieving data

### 4. Backend Service
**File**: `PanelService/AdminPanelService.cs`

#### Methods Added:
- **TrackLoginAsync(string employeeId)**: 
  - Tracks the first login of the day
  - Creates a new record if none exists for today
  - Does NOT update FirstLoginTime if record already exists (preserves first login)

- **TrackLogoutAsync(string employeeId)**:
  - Updates the LastLogoutTime for today's record
  - Only updates if a record exists for today

- **GetDailyLoginTrackingAsync(string? employeeId, DateTime? startDate, DateTime? endDate)**:
  - Retrieves login tracking data with optional filters
  - Returns list of DailyLoginTracking records

#### Login Method Updated:
- Now calls `TrackLoginAsync()` after successful authentication
- Includes EmployeeId in the response message

### 5. Frontend Components

#### Login Page
**File**: `Components/Pages/Registration&Login/Login.razor`
- Updated to extract and store EmployeeId in session storage
- Parses EmployeeId from login response message

#### Main Layout
**File**: `Components/Layout/MainLayout.razor`
- Updated Logout method to call `TrackLogoutAsync()` before clearing session
- Added link to Login Tracking page in admin menu

#### Login Tracking Page
**Files**: 
- `Components/Pages/LoginTracking/LoginTracking.razor`
- `Components/Pages/LoginTracking/LoginTracking.razor.css`

Features:
- Display all login tracking records in a table
- Filter by EmployeeId
- Filter by date range (Start Date, End Date)
- Calculate and display session duration
- Responsive design
- Loading states and error handling

## How It Works

### Login Flow:
1. User logs in through Login.razor
2. AdminPanelService.Login() validates credentials
3. If successful, TrackLoginAsync() is called
4. System checks if a record exists for today (EmployeeId + TrackingDate)
5. If no record exists, creates new record with FirstLoginTime = current time
6. If record exists, FirstLoginTime is NOT updated (preserves first login)
7. EmployeeId is stored in session storage

### Logout Flow:
1. User clicks logout in MainLayout.razor
2. TrackLogoutAsync() is called with EmployeeId from session
3. System finds today's record for the EmployeeId
4. Updates LastLogoutTime = current time
5. Session is cleared and user is redirected to login

### Data Retrieval:
1. Navigate to `/login-tracking` page
2. Page loads all records by default
3. User can filter by EmployeeId and/or date range
4. Data is displayed in a table with calculated durations

## SQL Queries

The `LoginTrackingQueries.sql` file contains 10 sample queries:
1. Get all records
2. Get records for specific employee
3. Get records for date range
4. Get records for employee within date range
5. Get today's records
6. Get currently logged in users
7. Calculate session durations
8. Get login statistics by employee
9. Get login statistics by date
10. Get longest sessions

## Setup Instructions

### 1. Create Database Table
Run the SQL script:
```sql
-- Execute: DbModels/CreateDailyLoginTrackingTable.sql
```

### 2. Build and Run
- The application should build without errors
- All dependencies are already configured

### 3. Test the System
1. Log in as a user
2. Check the database - a record should be created in DailyLoginTracking
3. Log out
4. Check the database - LastLogoutTime should be updated
5. Log in again the same day
6. Check the database - FirstLoginTime should NOT change, only LastLogoutTime should be cleared/null
7. Log out again
8. Check the database - LastLogoutTime should be updated again
9. Navigate to `/login-tracking` to view the data

## Key Features

✅ First login time is saved and never changes for the same day
✅ Multiple logins on the same day don't update FirstLoginTime
✅ Last logout time is updated every time user logs out
✅ User identified by EmployeeId (which maps to UserId in AdminInfo table)
✅ Data is retrievable through UI with filtering options
✅ SQL queries provided for direct database access
✅ Responsive UI design
✅ Error handling and loading states

## Notes

- EmployeeId in DailyLoginTracking corresponds to UserId in AdminInfo table
- The system uses UTC/local server time for tracking
- TrackingDate is stored as DATE type (date only, no time)
- FirstLoginTime and LastLogoutTime are stored as DATETIME2 (includes time)
- If a user logs out without logging in first (edge case), no record is created

## Future Enhancements (Optional)

- Add timezone support
- Add export to CSV/Excel functionality
- Add charts/graphs for login patterns
- Add email notifications for unusual login patterns
- Add automatic cleanup of old records
- Add pagination for large datasets
