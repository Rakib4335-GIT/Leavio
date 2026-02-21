# Dynamic Menu Management System - Implementation Guide

## Overview
This document describes the dynamic menu management system implemented for the Leavio application. The system allows administrators to manage menu items dynamically from the system, replacing the previously static menu structure.

## Database Design

### Table: MenuItem
- **Id**: INT (Primary Key, Identity)
- **Name**: NVARCHAR(100) (Required) - Display name of the menu item
- **Url**: NVARCHAR(500) (Required) - URL path for the menu item
- **Status**: BIT (Required, Default: 1) - Active/Inactive status
- **Icon**: NVARCHAR(500) (Nullable) - Icon class or SVG path
- **DisplayOrder**: INT (Required, Default: 0) - Order in which menu items appear
- **CreatedDate**: DATETIME2 (Required) - Creation timestamp
- **UpdatedDate**: DATETIME2 (Nullable) - Last update timestamp

### Index
- Composite index on (Status, DisplayOrder) for efficient queries

## Implementation Details

### 1. Database Model
**File**: `DbModels/MenuItem.cs`
- Entity class representing the MenuItem table
- Includes all required fields: Name, URL, Status, Icon, DisplayOrder

### 2. Database Context
**File**: `DbModels/RegesterServiceContext.cs`
- Added `DbSet<MenuItem>` property
- Configured entity mapping with proper indexes
- Set up field constraints and defaults

### 3. SQL Script
**File**: `DbModels/CreateMenuItemTable.sql`
- Creates the MenuItem table
- Creates necessary indexes
- Optionally inserts default menu items (Home, About Us, Contact)

### 4. Backend Service
**File**: `PanelService/AdminPanelService.cs`

#### Methods Added:
- **GetAllMenuItemsAsync()**: 
  - Retrieves all menu items ordered by DisplayOrder and Name
  - Returns list of MenuItem records

- **GetActiveMenuItemsAsync()**:
  - Retrieves only active menu items (Status = true)
  - Ordered by DisplayOrder and Name
  - Used for displaying menu in the navigation bar

- **GetMenuItemByIdAsync(int id)**:
  - Retrieves a single menu item by ID
  - Returns MenuItem or null

- **CreateMenuItemAsync(string name, string url, bool status, string? icon, int displayOrder)**:
  - Creates a new menu item
  - Validates input (name length, URL length)
  - Sets CreatedDate automatically

- **UpdateMenuItemAsync(int id, string name, string url, bool status, string? icon, int displayOrder)**:
  - Updates an existing menu item
  - Validates input
  - Sets UpdatedDate automatically

- **DeleteMenuItemAsync(int id)**:
  - Deletes a menu item by ID
  - Returns success/error response

### 5. Frontend Components

#### Menu Management Page
**Files**: 
- `Components/Pages/Menu/MenuManagement.razor`
- `Components/Pages/Menu/MenuManagement.razor.css`

Features:
- Display all menu items in a table
- Add new menu items
- Edit existing menu items
- Delete menu items
- Toggle status (Active/Inactive)
- Sort by Name, DisplayOrder, or Status
- Pagination support
- Responsive design
- Loading states and error handling
- Admin-only access

#### Main Layout
**File**: `Components/Layout/MainLayout.razor`
- Updated to load menu items dynamically from database
- Displays only active menu items (Status = true)
- Menu items are ordered by DisplayOrder
- Added "MENU MANAGEMENT" link to admin dropdown menu

## How It Works

### Menu Display Flow:
1. MainLayout.razor loads on page render
2. `LoadMenuItems()` is called in `OnAfterRenderAsync`
3. `GetActiveMenuItemsAsync()` retrieves active menu items from database
4. Menu items are displayed in the navigation bar
5. Menu items are ordered by DisplayOrder, then by Name

### Menu Management Flow:
1. Admin navigates to `/menu-management` page
2. Page loads all menu items (active and inactive)
3. Admin can:
   - Add new menu items via the form
   - Edit existing menu items (click Edit button)
   - Delete menu items (click Delete button with confirmation)
   - View status (Active/Inactive badge)
   - Sort and paginate through menu items

### Menu Item Fields:
- **Name**: Display name shown in navigation (required, max 100 chars)
- **URL**: Route path (required, max 500 chars, e.g., "/home", "/aboutUs")
- **Status**: Active/Inactive toggle (default: Active)
- **Icon**: Optional icon class or SVG path (max 500 chars)
- **DisplayOrder**: Numeric order for sorting (default: 0, lower numbers appear first)

## Setup Instructions

### 1. Create Database Table
Run the SQL script:
```sql
-- Execute: DbModels/CreateMenuItemTable.sql
```

### 2. Build and Run
- The application should build without errors
- All dependencies are already configured

### 3. Access Menu Management
1. Log in as an Admin user
2. Click on "Admin Menu" dropdown
3. Select "MENU MANAGEMENT"
4. Add, edit, or delete menu items as needed

### 4. Default Menu Items
The SQL script includes optional default menu items:
- Home (/)
- About Us (/aboutUs)
- Contact (/contact)

These can be modified or deleted after creation.

## Key Features

✅ Dynamic menu management from admin panel
✅ Add, Edit, Delete menu items
✅ Active/Inactive status toggle
✅ Display order control
✅ Icon support (optional)
✅ Admin-only access control
✅ Responsive UI design
✅ Pagination and sorting
✅ Error handling and validation
✅ SQL script for easy database setup

## Menu Item Management

### Adding a Menu Item:
1. Fill in the form:
   - Name: "Products"
   - URL: "/products"
   - Icon: "fa fa-shopping-cart" (optional)
   - Display Order: 3
   - Status: Active (checked)
2. Click "Add Menu Item"
3. Menu item appears in navigation bar immediately

### Editing a Menu Item:
1. Click "Edit" button on the menu item row
2. Form populates with existing values
3. Modify fields as needed
4. Click "Update Menu Item"
5. Changes reflect immediately

### Deleting a Menu Item:
1. Click "Delete" button on the menu item row
2. Confirm deletion in browser dialog
3. Menu item is removed from database
4. Menu item disappears from navigation bar

### Setting Display Order:
- Lower numbers appear first
- Items with the same order are sorted alphabetically by name
- Example: Order 0 = first, Order 1 = second, etc.

## Notes

- Menu items are cached per page load
- Changes to menu items require a page refresh to see in navigation (or implement real-time updates)
- Only active menu items (Status = true) are displayed in the navigation bar
- Menu management page shows all items (active and inactive)
- Icon field accepts any string (CSS class, SVG path, etc.)
- URL should start with "/" for internal routes

## Future Enhancements (Optional)

- Add menu item categories/submenus
- Add permission-based menu visibility
- Add menu item icons from icon library picker
- Add drag-and-drop for reordering
- Add menu item preview
- Add bulk operations (activate/deactivate multiple)
- Add menu item templates
- Add analytics tracking for menu clicks
