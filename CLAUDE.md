# QDeskPro - Quarry Management System

## Project Overview

QDeskPro is a modern, unified Blazor web application for quarry sales management, built on .NET 10. It consolidates daily sales operations for quarry clerks and backend analytics/reporting for managers into a single, streamlined application.

### Legacy System Context

The original QDesk system consisted of:
- **MAUI Mobile App**: Primary interface for clerks doing daily operations (sales entry, expenses, banking, fuel usage, daily reports)
- **Blazor Web App**: Backend interface for managers doing analytics and reporting
- **REST API**: Shared backend with Datasync capabilities

QDeskPro unifies both interfaces into a single responsive web application that works seamlessly on mobile devices (for clerks in the field) and desktops (for managers in the office).

**Key Simplifications from Legacy System:**
- No offline/sync requirements - always-online web application
- Single unified codebase instead of API + Web + MAUI + Shared libraries
- No mobile app distribution/update complexity
- Direct database access without sync layers

### Business Domain

QDeskPro manages the complete workflow of quarry operations:
- **Daily Sales Recording**: Clerks record sales transactions including vehicle registration, product type, quantity, pricing, and payment details
- **Expense Tracking**: Manual expenses, commissions (broker fees), loaders' fees, and land rate fees
- **Banking Records**: Track cash deposits and banking transactions
- **Fuel Usage Monitoring**: Track fuel consumption for machines and wheel loaders
- **Daily Notes**: End-of-day notes with closing balance tracking
- **Reports & Analytics**: Daily, weekly, and monthly sales reports with Excel export and email delivery

---

## Domain Models (Entities)

### BaseEntity (Abstract Base Class)

All entities inherit from this base class for consistent audit tracking:

```csharp
public abstract class BaseEntity
{
    public string Id { get; set; }           // GUID primary key
    public bool IsActive { get; set; }       // Soft delete flag
    public DateTime DateCreated { get; set; }
    public string CreatedBy { get; set; }    // User ID who created
    public DateTime? DateModified { get; set; }
    public string ModifiedBy { get; set; }   // User ID who modified
    public string DateStamp { get; set; }    // "yyyyMMdd" format for daily grouping
    public string QId { get; set; }          // Quarry ID for multi-tenant isolation
}
```

### ApplicationUser

Extends ASP.NET Identity User for quarry personnel:

```csharp
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; }     // Display name
    public string Position { get; set; }     // Job title/role description
    public string QuarryId { get; set; }     // Primary assigned quarry (for clerks)
    public bool IsActive { get; set; }       // Account active status

    // Navigation properties
    public virtual ICollection<Quarry> OwnedQuarries { get; set; }  // Quarries owned by this manager (one-to-many)
    public virtual ICollection<UserQuarry> QuarryAssignments { get; set; }  // Quarries assigned to this user
}
```

**Manager-Quarry Ownership Model:**
- **Administrators create Manager accounts** - Only admins can create managers
- **Managers can own multiple quarries** - One-to-many relationship via `Quarry.ManagerId`
- Each Quarry has exactly one owner (Manager) via `ManagerId` foreign key
- Managers have full control over quarries they own (settings, users, master data)
- Clerks are assigned to quarries via `UserQuarry` junction table

### Quarry

A quarry operation site with fee configurations:

```csharp
public class Quarry : BaseEntity
{
    public string QuarryName { get; set; }   // e.g., "Thika - Komu"
    public string Location { get; set; }     // Physical location
    public string ManagerId { get; set; }    // Owner/Manager who created this quarry
    public double? LoadersFee { get; set; }  // Per-unit fee for loaders (e.g., 50 KES)
    public double? LandRateFee { get; set; } // Per-unit land rate fee (e.g., 10 KES)
    public double? RejectsFee { get; set; }  // Alternative rate for Reject products (e.g., 5 KES)
    public string EmailRecipients { get; set; } // Comma-separated emails for reports
    public bool DailyReportEnabled { get; set; }
    public TimeSpan? DailyReportTime { get; set; }

    // Navigation
    public virtual ApplicationUser Manager { get; set; }  // Quarry owner
    public virtual ICollection<Layer> Layers { get; set; }
    public virtual ICollection<Broker> Brokers { get; set; }
    public virtual ICollection<ProductPrice> ProductPrices { get; set; }
    public virtual ICollection<UserQuarry> UserQuarries { get; set; }  // Assigned users
}
```

### Layer

Mining layer within a quarry (for tracking excavation progress):

```csharp
public class Layer : BaseEntity
{
    public string LayerLevel { get; set; }   // e.g., "Layer -1", "Layer -2"
    public DateTime? DateStarted { get; set; }
    public double? LayerLength { get; set; } // Optional: meters/feet
    public string QuarryId { get; set; }     // Foreign key

    // Navigation
    public virtual Quarry Quarry { get; set; }
}
```

### Product

Product types sold by quarries:

```csharp
public class Product : BaseEntity
{
    public string ProductName { get; set; }  // "Size 6", "Size 9", "Reject", etc.
    public string Description { get; set; }  // Optional description

    // Navigation
    public virtual ICollection<ProductPrice> Prices { get; set; }
}
```

### ProductPrice

Product pricing per quarry (allows different quarries to have different prices):

```csharp
public class ProductPrice : BaseEntity
{
    public string ProductId { get; set; }    // Foreign key
    public string QuarryId { get; set; }     // Foreign key
    public double Price { get; set; }        // Price per unit in KES

    // Navigation
    public virtual Product Product { get; set; }
    public virtual Quarry Quarry { get; set; }
}
```

### Broker

Sales brokers who earn commission on sales they facilitate:

```csharp
public class Broker : BaseEntity
{
    public string BrokerName { get; set; }   // Full name
    public string Phone { get; set; }        // Contact number
    public string quarryId { get; set; }     // Assigned quarry (lowercase for legacy compat)

    // Navigation
    public virtual Quarry Quarry { get; set; }
}
```

### Sale

Individual sale transaction (core business entity):

```csharp
public class Sale : BaseEntity
{
    // Transaction Details
    public DateTime? SaleDate { get; set; }
    public string VehicleRegistration { get; set; }  // Required: e.g., "KBZ 123A"

    // Client Details (optional)
    public string ClientName { get; set; }
    public string ClientPhone { get; set; }
    public string Destination { get; set; }  // Kenya county

    // Product Details
    public string ProductId { get; set; }    // Foreign key
    public string LayerId { get; set; }      // Foreign key
    public double Quantity { get; set; }     // Number of pieces
    public double PricePerUnit { get; set; } // Price at time of sale

    // Broker/Commission
    public string BrokerId { get; set; }     // Foreign key (nullable)
    public double CommissionPerUnit { get; set; }  // Commission per piece (0 if no broker)

    // Payment
    public string PaymentStatus { get; set; }    // "Paid" or "NotPaid"
    public string PaymentMode { get; set; }      // "Cash", "MPESA", "Bank Transfer"
    public string PaymentReference { get; set; } // Transaction reference

    // Clerk
    public string ApplicationUserId { get; set; }  // Clerk who recorded
    public string ClerkName { get; set; }          // Denormalized for reporting

    // Calculated (read-only, computed from above)
    public double GrossSaleAmount => Quantity * PricePerUnit;

    // Navigation
    public virtual Product Product { get; set; }
    public virtual Layer Layer { get; set; }
    public virtual Broker Broker { get; set; }
    public virtual ApplicationUser Clerk { get; set; }
}
```

### Expense

Manual expense entries by clerks:

```csharp
public class Expense : BaseEntity
{
    public DateTime? ExpenseDate { get; set; }
    public string Item { get; set; }          // Description of expense
    public double Amount { get; set; }        // Amount in KES
    public string Category { get; set; }      // Expense category
    public string TxnReference { get; set; }  // Payment reference
    public string ApplicationUserId { get; set; }  // Clerk who recorded
}
```

### Banking

Banking/deposit transactions:

```csharp
public class Banking : BaseEntity
{
    public DateTime? BankingDate { get; set; }
    public string Item { get; set; }          // Description (e.g., "Daily deposit")
    public double BalanceBF { get; set; }     // Balance brought forward (optional)
    public double AmountBanked { get; set; }  // Amount deposited
    public string TxnReference { get; set; }  // Bank reference number
    public string RefCode { get; set; }       // Short reference code
    public string ApplicationUserId { get; set; }
}
```

### FuelUsage

Daily fuel consumption tracking:

```csharp
public class FuelUsage : BaseEntity
{
    public DateTime? UsageDate { get; set; }
    public double OldStock { get; set; }       // Opening balance (liters)
    public double NewStock { get; set; }       // New fuel received (liters)
    public double MachinesLoaded { get; set; } // Fuel used by machines
    public double WheelLoadersLoaded { get; set; }  // Fuel used by wheel loaders
    public string ApplicationUserId { get; set; }

    // Calculated properties
    public double TotalStock => OldStock + NewStock;
    public double Used => MachinesLoaded + WheelLoadersLoaded;
    public double Balance => TotalStock - Used;
}
```

### DailyNote

End-of-day notes and closing balance:

```csharp
public class DailyNote : BaseEntity
{
    public DateTime? NoteDate { get; set; }
    public string Notes { get; set; }         // Free-text notes
    public double ClosingBalance { get; set; } // Cash in hand at end of day
    public string quarryId { get; set; }      // Quarry ID (lowercase legacy)
}
```

### UserQuarry

Many-to-many relationship for user quarry assignments:

```csharp
public class UserQuarry : BaseEntity
{
    public string UserId { get; set; }
    public string QuarryId { get; set; }
    public bool IsPrimary { get; set; }  // Primary assignment

    // Navigation
    public virtual ApplicationUser User { get; set; }
    public virtual Quarry Quarry { get; set; }
}
```

---

## Complete Workflows

### Workflow 1: New Sale Entry

**Actor**: Clerk
**Trigger**: Clerk taps "New Sale" button or navigates to sales entry

**Steps**:

1. **Initialize Form**
   - Load layers for clerk's quarry (show last 3 layers)
   - Load products from master data
   - Load brokers for clerk's quarry
   - Set default date to today
   - Set minimum date to 14 days ago

2. **Clerk Selects Product**
   - System loads ProductPrice for selected product + quarry
   - Auto-fills PricePerUnit

3. **Clerk Selects Layer**
   - Layer picker shows layers ordered by DateStarted DESC
   - Last 3 layers typically shown

4. **Clerk Enters Sale Details**
   - Vehicle Registration (required, text input)
   - Sale Date (date picker, max today, min 14 days ago)
   - Quantity (numeric input, required)

5. **Clerk Expands Client Details (Optional)**
   - Client Name
   - Client Phone
   - Destination (Kenya counties dropdown)

6. **Clerk Selects Broker (Optional)**
   - If broker selected, CommissionPerUnit input becomes active
   - Clerk enters commission rate

7. **Clerk Selects Payment Details**
   - Payment Mode: Cash, MPESA, or Bank Transfer
   - Payment Status: Paid or Not Paid
   - Payment Reference (text for MPESA code, bank ref, etc.)

8. **System Calculates Order Summary** (real-time as user types)
   ```
   Total Amount    = Quantity × PricePerUnit
   Commission      = Quantity × CommissionPerUnit
   Loaders Fee     = Quantity × Quarry.LoadersFee
   Land Rate Fee   = Quantity × Quarry.LandRateFee (or RejectsFee for Reject)
   Net Amount      = Total - Commission - LoadersFee - LandRateFee
   ```

9. **Clerk Submits**
   - Validation runs (required fields, positive values)
   - Confirmation dialog shows summary
   - On confirm, sale is saved with:
     - DateStamp = SaleDate.ToString("yyyyMMdd")
     - QId = Clerk's quarry ID
     - ApplicationUserId = Clerk's user ID
     - DateCreated = now

10. **Post-Save**
    - Success notification shown
    - Form resets for next entry
    - Dashboard updates to reflect new sale

---

### Workflow 2: Expense Entry

**Actor**: Clerk
**Trigger**: Clerk navigates to Expenses page

**Steps**:

1. **View Existing Expenses**
   - System loads clerk's expenses for last 14 days
   - Displayed in paginated list, newest first

2. **Add New Expense**
   - Item: Description of expense (required)
   - Amount: Numeric value in KES (required, > 0)
   - Payment Reference: Transaction reference (optional)
   - Expense Date: Date picker (min 14 days ago, max today)
   - Category: Select from predefined list

3. **Submit**
   - Validation runs
   - Expense saved with clerk's user ID and quarry ID

4. **Edit Existing** (tap on expense item)
   - Form populates with expense data
   - Update/Delete buttons become visible
   - Cancel returns to clean state

---

### Workflow 3: Banking Entry

**Actor**: Clerk
**Trigger**: Clerk navigates to Banking page

**Steps**:

1. **View Banking Records**
   - Load clerk's banking records for last 14 days
   - Displayed with amount banked and reference

2. **Add Banking Record**
   - Banking Date (required)
   - Amount Banked (required, >= 0)
   - Transaction Reference (for bank statement reconciliation)

3. **Submit**
   - Record saved with clerk credentials

---

### Workflow 4: Fuel Usage Tracking

**Actor**: Clerk
**Trigger**: Clerk navigates to Fuel Usage page

**Steps**:

1. **View Fuel History**
   - Show last 14 days of fuel records
   - Display: Date, Old Stock, New Stock, Used, Balance

2. **Record Daily Usage**
   - Old Stock: Opening balance (liters B/F)
   - New Stock: New fuel received today
   - Machines Loaded: Fuel dispensed to machines
   - Wheel Loaders Loaded: Fuel dispensed to wheel loaders
   - Usage Date

3. **System Calculates**
   ```
   Total Stock = Old Stock + New Stock
   Balance = Total Stock - Machines - WheelLoaders
   ```

4. **Submit**
   - Record saved
   - Balance becomes next day's Old Stock reference

---

### Workflow 5: Daily Sales Report Generation

**Actor**: Clerk or Manager
**Trigger**: User navigates to Reports page

**Steps**:

1. **Select Date Range**
   - From Date picker
   - To Date picker
   - For single-day report: From = To = selected date

2. **Generate Report** (on button click)

3. **System Loads Data**:
   - **Sales**: All sales in date range for quarry/clerk
     - Calculate: TotalQuantity, TotalSales, UnpaidAmount

   - **Expenses**: Generate expense items from 4 sources:
     a. User manual expenses
     b. Commission expenses (derived from sales with commission)
     c. Loaders fee expenses (derived from all sales)
     d. Land rate expenses (derived from all sales)

   - **Banking**: All banking records in date range

   - **Fuel Usage**: All fuel records in date range

4. **Calculate Summary**:
   ```
   // For single-day reports only
   OpeningBalance = Previous day's DailyNote.ClosingBalance (or 0)

   TotalSales = Sum of all sale amounts
   TotalExpenses = Sum of all expense items (all 4 types)

   Commission = Sum where LineType = "Commission Expense"
   LoadersFee = Sum where LineType = "Loaders Fee Expense"
   LandRateFee = Sum where LineType = "Land Rate Fee Expense"

   Earnings = TotalSales - TotalExpenses
   UnpaidOrders = Sum of sales where PaymentStatus = "NotPaid"
   NetEarnings = (Earnings + OpeningBalance) - UnpaidOrders
   Banked = Sum of banking amounts
   CashInHand = NetEarnings - Banked
   ```

5. **Display Report**:
   - Sales table with product, quantity, amount, paid status
   - Expenses table with type breakdown
   - Fuel usage table
   - Banking table
   - Summary section with all calculated values

6. **Save Closing Balance** (single-day reports):
   - Automatically save CashInHand to DailyNote.ClosingBalance
   - This becomes next day's opening balance

7. **Export/Share**:
   - Share as text (formatted summary)
   - Export to Excel (multi-worksheet)
   - Email to configured recipients

---

### Workflow 6: Manager Analytics Dashboard

**Actor**: Manager or Administrator
**Trigger**: Login or navigate to Dashboard

**Steps**:

1. **Select Parameters**
   - Quarry selector (managers see all, clerks see assigned)
   - Date range (From/To)

2. **Load Dashboard Data**:
   - Total Revenue (sum of all sales)
   - Total Orders (count of sales)
   - Total Quantity (sum of quantities)
   - Total Fuel Consumed

3. **Display Visualizations**:
   - Metric cards with averages
   - Sales vs Expenses trend chart
   - Profit margin gauge
   - Daily breakdown table

4. **Drill-down**:
   - Click on date to see individual sales
   - Click on metric for detailed breakdown

---

### Workflow 7: Master Data Management

**Actor**: Administrator
**Trigger**: Navigate to Admin > Master Data

**Quarry Management**:
- Add/Edit quarry name, location
- Configure fees (LoadersFee, LandRateFee, RejectsFee)
- Set email recipients for reports
- Enable/configure daily report schedule

**Layer Management**:
- Add new layer when excavation moves to new level
- Set layer level name, start date
- Associate with quarry

**Broker Management**:
- Add brokers with name and phone
- Assign to specific quarry
- Activate/deactivate brokers

**Product Pricing**:
- Set prices per product per quarry
- Update prices (creates audit trail)

---

### Workflow 8: User Management

**Actor**: Administrator
**Trigger**: Navigate to Admin > Users

**Steps**:

1. **View Users**
   - List all users with role and quarry assignment
   - Show active/inactive status

2. **Add User**
   - Full Name
   - Email
   - Password (initial)
   - Role: Administrator, Manager, or Clerk
   - Assign to quarry (required for Clerk)

3. **Edit User**
   - Update profile details
   - Change role
   - Reassign quarry
   - Reset password

4. **Deactivate User**
   - Soft delete (IsActive = false)
   - User cannot login but data preserved

### User Roles & Hierarchy

QDeskPro implements a hierarchical user management system with three distinct roles:

#### Role Hierarchy

```
Administrator
    └── Creates/Manages → Managers
                              └── Creates/Manages → Clerks
                              └── Creates/Manages → Quarries (owns)
                              └── Assigns → Clerks to Quarries
```

#### Role Definitions

| Role | Description | Permissions |
|------|-------------|-------------|
| **Administrator** | System administrator | Create managers only; Full system access; View all data across all quarries |
| **Manager** | Quarry owner/manager | Create and manage own quarries; Add other managers and clerks to own quarries; View analytics and reports across own quarries; Manage master data (products, layers, brokers, prices) for own quarries |
| **Clerk** | Field operator | Capture daily operations (sales, expenses, banking, fuel usage); Generate sales reports; Access only assigned quarry data |

#### Access Control Matrix

| Feature | Administrator | Manager | Clerk |
|---------|--------------|---------|-------|
| Create Managers | ✅ | ❌ | ❌ |
| Create Clerks | ❌ | ✅ (own quarries) | ❌ |
| Create Quarries | ❌ | ✅ | ❌ |
| Manage Quarry Settings | ❌ | ✅ (own quarries) | ❌ |
| Assign Users to Quarries | ❌ | ✅ (own quarries) | ❌ |
| View All Quarries | ✅ | ❌ | ❌ |
| View Own Quarries | ✅ | ✅ | ✅ (assigned only) |
| Analytics Dashboard | ✅ | ✅ (own quarries) | ❌ |
| Capture Sales | ❌ | ❌ | ✅ |
| Capture Expenses | ❌ | ❌ | ✅ |
| Capture Banking | ❌ | ❌ | ✅ |
| Capture Fuel Usage | ❌ | ❌ | ✅ |
| Generate Reports | ✅ | ✅ (own quarries) | ✅ (own data) |
| Manage Products | ❌ | ✅ (own quarries) | ❌ |
| Manage Layers | ❌ | ✅ (own quarries) | ❌ |
| Manage Brokers | ❌ | ✅ (own quarries) | ❌ |
| Manage Prices | ❌ | ✅ (own quarries) | ❌ |

#### Quarry Ownership Model

```csharp
// Quarry entity includes ManagerId to track ownership
public class Quarry : BaseEntity
{
    public string ManagerId { get; set; }  // The manager who created/owns this quarry
    // ... other properties

    // Navigation
    public virtual ApplicationUser Manager { get; set; }
}
```

#### User-Quarry Assignment

```csharp
// UserQuarry junction table for assigning users (clerks/managers) to quarries
public class UserQuarry : BaseEntity
{
    public string UserId { get; set; }      // ApplicationUser ID
    public string QuarryId { get; set; }    // Quarry ID
    public string AssignedBy { get; set; }  // Manager who made the assignment
    public DateTime AssignedDate { get; set; }

    // Navigation
    public virtual ApplicationUser User { get; set; }
    public virtual Quarry Quarry { get; set; }
}
```

#### Authorization Policies

```csharp
// Program.cs - Authorization policy configuration
builder.Services.AddAuthorization(options =>
{
    // Administrator-only actions
    options.AddPolicy("RequireAdministrator", policy =>
        policy.RequireRole("Administrator"));

    // Manager or Administrator
    options.AddPolicy("RequireManagerOrAdmin", policy =>
        policy.RequireRole("Administrator", "Manager"));

    // Clerk operations
    options.AddPolicy("RequireClerk", policy =>
        policy.RequireRole("Clerk"));

    // Any authenticated user
    options.AddPolicy("RequireAuthenticated", policy =>
        policy.RequireAuthenticatedUser());

    // Custom policy for quarry access
    options.AddPolicy("RequireQuarryAccess", policy =>
        policy.Requirements.Add(new QuarryAccessRequirement()));
});
```

#### Quarry Access Authorization Handler

```csharp
public class QuarryAccessHandler : AuthorizationHandler<QuarryAccessRequirement, string>
{
    private readonly AppDbContext _db;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        QuarryAccessRequirement requirement,
        string quarryId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;

        // Administrators have access to all quarries
        if (userRole == "Administrator")
        {
            context.Succeed(requirement);
            return;
        }

        // Managers have access to quarries they own or are assigned to
        if (userRole == "Manager")
        {
            var hasAccess = await _db.Quarries.AnyAsync(q =>
                q.Id == quarryId && q.ManagerId == userId) ||
                await _db.UserQuarries.AnyAsync(uq =>
                    uq.UserId == userId && uq.QuarryId == quarryId);

            if (hasAccess) context.Succeed(requirement);
            return;
        }

        // Clerks have access only to assigned quarries
        if (userRole == "Clerk")
        {
            var hasAccess = await _db.UserQuarries.AnyAsync(uq =>
                uq.UserId == userId && uq.QuarryId == quarryId);

            if (hasAccess) context.Succeed(requirement);
        }
    }
}
```

#### Navigation Based on Role

```csharp
// Role-based navigation items
public static class NavigationHelper
{
    public static List<NavItem> GetNavigationItems(string role)
    {
        return role switch
        {
            "Administrator" => new List<NavItem>
            {
                new("/admin/managers", "Managers", Icons.Material.Filled.SupervisorAccount),
                new("/admin/quarries", "All Quarries", Icons.Material.Filled.Terrain),
                new("/reports", "Reports", Icons.Material.Filled.Assessment),
            },
            "Manager" => new List<NavItem>
            {
                new("/dashboard", "Analytics", Icons.Material.Filled.Dashboard),
                new("/quarries", "My Quarries", Icons.Material.Filled.Terrain),
                new("/users", "Users", Icons.Material.Filled.People),
                new("/reports", "Reports", Icons.Material.Filled.Assessment),
                new("/master-data", "Master Data", Icons.Material.Filled.Settings),
            },
            "Clerk" => new List<NavItem>
            {
                new("/clerk/dashboard", "Dashboard", Icons.Material.Filled.Dashboard),
                new("/clerk/sales/new", "New Sale", Icons.Material.Filled.AddShoppingCart),
                new("/clerk/expenses", "Expenses", Icons.Material.Filled.Receipt),
                new("/clerk/banking", "Banking", Icons.Material.Filled.AccountBalance),
                new("/clerk/fuel", "Fuel Usage", Icons.Material.Filled.LocalGasStation),
                new("/clerk/reports", "Reports", Icons.Material.Filled.Assessment),
            },
            _ => new List<NavItem>()
        };
    }
}
```

---

## Architecture

### Technology Stack

- **Framework**: .NET 10 with Blazor Server (Pure Server-Side Rendering with Interactive Server mode)
- **UI Library**: MudBlazor v8+ for modern component-based UI
- **Database**: SQL Server with Entity Framework Core 10
- **Authentication**: ASP.NET Core Identity with cookie-based authentication
- **API Pattern**: Minimal APIs with strongly-typed endpoints
- **Real-time**: SignalR for live dashboard updates
- **Reporting**: ClosedXML for Excel export, QuestPDF for PDF generation

### Project Structure

```
QDeskPro/
├── QDeskPro.sln
├── src/
│   └── QDeskPro/                      # Unified Blazor Web App
│       ├── QDeskPro.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       │
│       ├── Data/                       # Database Layer
│       │   ├── AppDbContext.cs
│       │   ├── Migrations/
│       │   └── Seed/
│       │
│       ├── Domain/                     # Core Domain
│       │   ├── Entities/              # Domain entities
│       │   ├── Enums/                 # PaymentMode, PaymentStatus, UserRole
│       │   └── Services/              # Business logic services
│       │
│       ├── Features/                   # Feature-based organization
│       │   ├── Sales/
│       │   │   ├── Components/
│       │   │   ├── Pages/
│       │   │   └── Services/
│       │   ├── Expenses/
│       │   ├── Banking/
│       │   ├── FuelUsage/
│       │   ├── Reports/
│       │   ├── Dashboard/
│       │   └── MasterData/
│       │
│       ├── Shared/                     # Shared components
│       │   ├── Components/
│       │   ├── Layouts/
│       │   └── Extensions/
│       │
│       ├── Api/                        # Minimal API endpoints
│       │   └── Endpoints/
│       │
│       └── wwwroot/
│           ├── css/                   # Application styles
│           ├── js/                    # JavaScript utilities (charts, etc.)
│           └── icons/                 # Favicon and app icons
│
└── tests/
    └── QDeskPro.Tests/
```

---

## Business Logic & Workflows

### Sale Recording Workflow

1. Clerk selects: Sale Date, Layer, Product, Broker (optional)
2. Clerk enters: Vehicle Registration, Client Name (optional), Quantity
3. System auto-fills: Price per unit (from ProductPrice), Commission per unit
4. Clerk enters: Payment Mode (MPESA/Cash/Bank), Payment Reference
5. System calculates:
   - **Gross Amount** = Quantity × Price per Unit
   - **Commission** = Quantity × Commission per Unit
   - **Loaders Fee** = Quantity × Quarry.LoadersFee
   - **Land Rate Fee** = Quantity × Quarry.LandRateFee (or Quarry.RejectsFee for Reject product)
   - **Net Amount** = Gross - Commission - LoadersFee - LandRateFee
6. Sale is saved with DateStamp (yyyyMMdd format) for daily grouping

### Expense Categories

Expenses are categorized as:
- **Manual Expenses**: User-entered operational expenses
- **Commission**: Auto-calculated from sales with commission > 0
- **Loaders Fees**: Auto-calculated per sale (Quantity × LoadersFee)
- **Land Rate**: Auto-calculated per sale (uses RejectsFee for Reject products)

### Daily Sales Summary

Daily summaries aggregate:
- Total Orders Count
- Total Quantity Sold
- Total Sales Amount (gross)
- Total Expenses (manual + commissions + loaders + land rate)
- Net Amount (Sales - Expenses)

### Report Generation

Reports are generated for date ranges with:
- Sales worksheet: Date, Client, Product, Quantity/Price, Reference, Amount
- Expenses worksheet: Date, Description, Amount (including auto-calculated fees)
- Fuel Usage worksheet: Date, Old/New Stock, Total, Machines, W/Loaders, Balance
- Total calculation: Sales - Expenses

### Scheduled Reports

Quarries can configure:
- Daily report delivery (with configurable time)
- Email recipients (comma-separated)
- Reports sent as Excel attachments

---

## Validation Rules

### Sale Validation
- SaleDate: Required, cannot be more than 14 days in the past
- VehicleRegistration: Required, not empty
- Quantity: Required, must be > 0
- PricePerUnit: Required, must be > 0
- LayerId: Required
- ProductId: Required
- PaymentStatus: Required (Paid/NotPaid)
- PaymentMode: Required (MPESA/Cash/Bank)

### Expense Validation
- ExpenseDate: Required
- Item: Required, not empty
- Amount: Required, must be > 0

### Banking Validation
- BankingDate: Required
- Item: Required
- AmountBanked: Required, must be >= 0

### Fuel Usage Validation
- UsageDate: Required
- All numeric fields: Must be >= 0

### Daily Note Validation
- NoteDate: Required
- quarryId: Required (must match user's assigned quarry)
- Notes: Optional, max 1000 characters
- ClosingBalance: Automatically calculated from report, not user-editable directly

---

## Comprehensive Data Capture Logic

### Sale Capture - Complete Logic

**Form Initialization:**
```csharp
// On page load
async Task OnInitializedAsync()
{
    // Load master data for user's quarry
    Products = await GetProductsAsync();
    Layers = await GetQuarryLayersAsync(UserQuarryId)
        .OrderByDescending(l => l.DateStarted)
        .Take(3);  // Show only last 3 layers
    Brokers = await GetQuarryBrokersAsync(UserQuarryId);
    Quarry = await GetQuarryAsync(UserQuarryId);

    // Set defaults
    Sale.SaleDate = DateTime.Today;
    Sale.PaymentStatus = "Paid";
    Sale.PaymentMode = "Cash";
}
```

**Product Selection Handler:**
```csharp
async Task OnProductSelected(string productId)
{
    Sale.ProductId = productId;

    // Auto-load price for this product at this quarry
    var productPrice = await GetProductPriceAsync(productId, UserQuarryId);
    if (productPrice != null)
    {
        Sale.PricePerUnit = productPrice.Price;
    }

    // Trigger order summary recalculation
    CalculateOrderSummary();
}
```

**Order Summary Calculation (Real-time):**
```csharp
void CalculateOrderSummary()
{
    // Basic calculations
    TotalAmount = Sale.Quantity * Sale.PricePerUnit;
    CommissionAmount = Sale.Quantity * Sale.CommissionPerUnit;

    // Loaders fee (if configured for quarry)
    LoadersFeeAmount = Quarry.LoadersFee.HasValue
        ? Sale.Quantity * Quarry.LoadersFee.Value
        : 0;

    // Land rate fee (with Reject product special handling)
    var product = Products.FirstOrDefault(p => p.Id == Sale.ProductId);
    if (Quarry.LandRateFee.HasValue && Quarry.LandRateFee > 0)
    {
        if (product?.ProductName.ToLower().Contains("reject") == true)
        {
            LandRateFeeAmount = Sale.Quantity * (Quarry.RejectsFee ?? 0);
        }
        else
        {
            LandRateFeeAmount = Sale.Quantity * Quarry.LandRateFee.Value;
        }
    }
    else
    {
        LandRateFeeAmount = 0;
    }

    // Net amount
    NetAmount = TotalAmount - CommissionAmount - LoadersFeeAmount - LandRateFeeAmount;
}
```

**Sale Submission:**
```csharp
async Task SubmitSale()
{
    // Validation
    if (!await ValidateSale()) return;

    // Set audit fields
    Sale.Id = Guid.NewGuid().ToString();
    Sale.DateStamp = Sale.SaleDate.Value.ToString("yyyyMMdd");
    Sale.QId = UserQuarryId;
    Sale.ApplicationUserId = CurrentUserId;
    Sale.ClerkName = CurrentUserFullName;
    Sale.DateCreated = DateTime.UtcNow;
    Sale.CreatedBy = CurrentUserId;
    Sale.IsActive = true;

    // Save
    await SaveSaleAsync(Sale);

    // Success feedback
    ShowSuccessNotification("Sale recorded successfully");

    // Reset form for next entry
    ResetForm();
}

async Task<bool> ValidateSale()
{
    var errors = new List<string>();

    // Required fields
    if (string.IsNullOrWhiteSpace(Sale.VehicleRegistration))
        errors.Add("Vehicle Registration is required");

    if (Sale.SaleDate == null)
        errors.Add("Sale Date is required");

    if (string.IsNullOrWhiteSpace(Sale.ProductId))
        errors.Add("Product is required");

    if (string.IsNullOrWhiteSpace(Sale.LayerId))
        errors.Add("Layer is required");

    if (Sale.Quantity <= 0)
        errors.Add("Quantity must be greater than 0");

    if (Sale.PricePerUnit <= 0)
        errors.Add("Price per unit must be greater than 0");

    // Date validation (max 14 days back)
    if (Sale.SaleDate < DateTime.Today.AddDays(-14))
        errors.Add("Cannot backdate sale more than 14 days");

    if (Sale.SaleDate > DateTime.Today)
        errors.Add("Sale date cannot be in the future");

    // Payment validation
    if (string.IsNullOrWhiteSpace(Sale.PaymentStatus))
        errors.Add("Payment Status is required");

    if (string.IsNullOrWhiteSpace(Sale.PaymentMode))
        errors.Add("Payment Mode is required");

    // Commission validation (if broker selected, commission should be > 0)
    if (!string.IsNullOrWhiteSpace(Sale.BrokerId) && Sale.CommissionPerUnit <= 0)
    {
        // Warning only - broker can have 0 commission in some cases
    }

    if (errors.Any())
    {
        ShowValidationErrors(errors);
        return false;
    }

    return true;
}
```

### Expense Capture - Complete Logic

**Form Initialization:**
```csharp
async Task OnInitializedAsync()
{
    // Load existing expenses for last 14 days
    var cutoffDate = DateTime.Today.AddDays(-14);
    Expenses = await GetExpensesAsync()
        .Where(e => e.ApplicationUserId == CurrentUserId)
        .Where(e => e.ExpenseDate >= cutoffDate)
        .OrderByDescending(e => e.ExpenseDate)
        .ToListAsync();

    // Set defaults
    Expense.ExpenseDate = DateTime.Today;
    Expense.Category = "Miscellaneous";
}
```

**Expense Submission:**
```csharp
async Task SubmitExpense()
{
    // Validation
    var errors = new List<string>();

    if (string.IsNullOrWhiteSpace(Expense.Item))
        errors.Add("Item description is required");

    if (Expense.Amount <= 0)
        errors.Add("Amount must be greater than 0");

    if (Expense.ExpenseDate == null)
        errors.Add("Expense Date is required");

    if (Expense.ExpenseDate < DateTime.Today.AddDays(-14))
        errors.Add("Cannot backdate expense more than 14 days");

    if (Expense.ExpenseDate > DateTime.Today)
        errors.Add("Expense date cannot be in the future");

    if (errors.Any())
    {
        ShowValidationErrors(errors);
        return;
    }

    // Set audit fields
    if (Expense.Id == null)  // New expense
    {
        Expense.Id = Guid.NewGuid().ToString();
        Expense.DateCreated = DateTime.UtcNow;
        Expense.CreatedBy = CurrentUserId;
    }
    else  // Update existing
    {
        Expense.DateModified = DateTime.UtcNow;
        Expense.ModifiedBy = CurrentUserId;
    }

    Expense.DateStamp = Expense.ExpenseDate.Value.ToString("yyyyMMdd");
    Expense.QId = UserQuarryId;
    Expense.ApplicationUserId = CurrentUserId;
    Expense.IsActive = true;

    await SaveExpenseAsync(Expense);
    ShowSuccessNotification("Expense saved successfully");
    await RefreshExpenseList();
    ResetForm();
}
```

**Expense Edit/Delete:**
```csharp
void SelectExpenseForEdit(Expense expense)
{
    // Copy to form
    Expense = expense.Clone();
    IsEditMode = true;
}

async Task DeleteExpense()
{
    if (!await ConfirmDialog("Delete this expense?")) return;

    await RemoveExpenseAsync(Expense);
    ShowSuccessNotification("Expense deleted");
    await RefreshExpenseList();
    ResetForm();
}
```

### Banking Capture - Complete Logic

**Form Initialization:**
```csharp
async Task OnInitializedAsync()
{
    // Load existing banking records for last 14 days
    var cutoffDate = DateTime.Today.AddDays(-14);
    Bankings = await GetBankingsAsync()
        .Where(b => b.ApplicationUserId == CurrentUserId)
        .Where(b => b.BankingDate >= cutoffDate)
        .OrderByDescending(b => b.BankingDate)
        .ToListAsync();

    // Set defaults
    Banking.BankingDate = DateTime.Today;
    Banking.Item = "Daily Deposit";  // Common default
}
```

**Banking Submission:**
```csharp
async Task SubmitBanking()
{
    var errors = new List<string>();

    if (Banking.BankingDate == null)
        errors.Add("Banking Date is required");

    if (Banking.AmountBanked < 0)
        errors.Add("Amount banked cannot be negative");

    if (Banking.BankingDate < DateTime.Today.AddDays(-14))
        errors.Add("Cannot backdate banking more than 14 days");

    if (Banking.BankingDate > DateTime.Today)
        errors.Add("Banking date cannot be in the future");

    if (errors.Any())
    {
        ShowValidationErrors(errors);
        return;
    }

    // Set audit fields
    if (Banking.Id == null)
    {
        Banking.Id = Guid.NewGuid().ToString();
        Banking.DateCreated = DateTime.UtcNow;
        Banking.CreatedBy = CurrentUserId;
    }
    else
    {
        Banking.DateModified = DateTime.UtcNow;
        Banking.ModifiedBy = CurrentUserId;
    }

    Banking.DateStamp = Banking.BankingDate.Value.ToString("yyyyMMdd");
    Banking.QId = UserQuarryId;
    Banking.ApplicationUserId = CurrentUserId;
    Banking.IsActive = true;

    // Generate short reference code from TxnReference
    if (!string.IsNullOrWhiteSpace(Banking.TxnReference) && Banking.TxnReference.Length > 10)
    {
        Banking.RefCode = Banking.TxnReference.Substring(0, 10);
    }
    else
    {
        Banking.RefCode = Banking.TxnReference;
    }

    await SaveBankingAsync(Banking);
    ShowSuccessNotification("Banking record saved");
    await RefreshBankingList();
    ResetForm();
}
```

### Fuel Usage Capture - Complete Logic

**Form Initialization:**
```csharp
async Task OnInitializedAsync()
{
    // Load fuel usage history for last 14 days
    var cutoffDate = DateTime.Today.AddDays(-14);
    FuelUsages = await GetFuelUsagesAsync()
        .Where(f => f.UsageDate >= cutoffDate)
        .OrderByDescending(f => f.UsageDate)
        .ToListAsync();

    // Set defaults
    FuelUsage.UsageDate = DateTime.Today;

    // Try to get previous day's balance as today's OldStock
    var previousUsage = await GetFuelUsageByDateStampAsync(DateTime.Today.AddDays(-1).ToString("yyyyMMdd"));
    if (previousUsage != null)
    {
        FuelUsage.OldStock = previousUsage.Balance;
    }
}
```

**Real-time Calculation:**
```csharp
void CalculateFuelBalance()
{
    TotalStock = FuelUsage.OldStock + FuelUsage.NewStock;
    Used = FuelUsage.MachinesLoaded + FuelUsage.WheelLoadersLoaded;
    Balance = TotalStock - Used;
}
```

**Fuel Usage Submission:**
```csharp
async Task SubmitFuelUsage()
{
    var errors = new List<string>();

    if (FuelUsage.UsageDate == null)
        errors.Add("Usage Date is required");

    if (FuelUsage.OldStock < 0)
        errors.Add("Old Stock cannot be negative");

    if (FuelUsage.NewStock < 0)
        errors.Add("New Stock cannot be negative");

    if (FuelUsage.MachinesLoaded < 0)
        errors.Add("Machines Loaded cannot be negative");

    if (FuelUsage.WheelLoadersLoaded < 0)
        errors.Add("Wheel Loaders Loaded cannot be negative");

    // Check balance isn't negative
    CalculateFuelBalance();
    if (Balance < 0)
        errors.Add("Fuel usage exceeds available stock");

    if (errors.Any())
    {
        ShowValidationErrors(errors);
        return;
    }

    // Check if record exists for this date
    var existingUsage = await GetFuelUsageByDateStampAsync(FuelUsage.UsageDate.Value.ToString("yyyyMMdd"));

    if (existingUsage != null && FuelUsage.Id == null)
    {
        // Update existing record instead of creating new
        FuelUsage.Id = existingUsage.Id;
    }

    // Set audit fields
    if (FuelUsage.Id == null)
    {
        FuelUsage.Id = Guid.NewGuid().ToString();
        FuelUsage.DateCreated = DateTime.UtcNow;
        FuelUsage.CreatedBy = CurrentUserId;
    }
    else
    {
        FuelUsage.DateModified = DateTime.UtcNow;
        FuelUsage.ModifiedBy = CurrentUserId;
    }

    FuelUsage.DateStamp = FuelUsage.UsageDate.Value.ToString("yyyyMMdd");
    FuelUsage.QId = UserQuarryId;
    FuelUsage.ApplicationUserId = CurrentUserId;
    FuelUsage.IsActive = true;

    await SaveFuelUsageAsync(FuelUsage);
    ShowSuccessNotification("Fuel usage saved");
    await RefreshFuelUsageList();
}
```

### Daily Notes Capture - Complete Logic

**Dashboard Integration:**
```csharp
// Daily notes are typically captured on the Dashboard page
async Task OnInitializedAsync()
{
    // Load today's note if exists
    var todayNote = await GetDailyNoteByDateStampAsync(
        DateTime.Today.ToString("yyyyMMdd"),
        UserQuarryId
    );

    if (todayNote != null)
    {
        DailyNote = todayNote;
    }
    else
    {
        DailyNote = new DailyNote
        {
            NoteDate = DateTime.Today,
            quarryId = UserQuarryId
        };
    }
}

async Task SaveDailyNote()
{
    if (DailyNote.Id == null)
    {
        DailyNote.Id = Guid.NewGuid().ToString();
        DailyNote.DateCreated = DateTime.UtcNow;
        DailyNote.CreatedBy = CurrentUserId;
    }
    else
    {
        DailyNote.DateModified = DateTime.UtcNow;
        DailyNote.ModifiedBy = CurrentUserId;
    }

    DailyNote.DateStamp = DailyNote.NoteDate.Value.ToString("yyyyMMdd");
    DailyNote.QId = UserQuarryId;
    DailyNote.IsActive = true;

    // Note: ClosingBalance is set by report generation, not manually
    await SaveDailyNoteAsync(DailyNote);
    ShowSuccessNotification("Note saved");
}
```

---

## Data Filtering & Display Rules

### Sales List Display
```csharp
// Show only sales from last 14 days for the current clerk
var cutoffDate = DateTime.Today.AddDays(-14);
var sales = await Sales
    .Where(s => s.ApplicationUserId == CurrentUserId)
    .Where(s => s.SaleDate >= cutoffDate)
    .OrderByDescending(s => s.SaleDate)
    .ThenByDescending(s => s.DateCreated)
    .ToListAsync();
```

### Expenses List Display
```csharp
// Show only expenses from last 14 days for the current clerk
var cutoffDate = DateTime.Today.AddDays(-14);
var expenses = await Expenses
    .Where(e => e.ApplicationUserId == CurrentUserId)
    .Where(e => e.ExpenseDate >= cutoffDate)
    .OrderByDescending(e => e.ExpenseDate)
    .ToListAsync();
```

### Banking List Display
```csharp
// Show only banking from last 14 days for the current clerk
var cutoffDate = DateTime.Today.AddDays(-14);
var bankings = await Bankings
    .Where(b => b.ApplicationUserId == CurrentUserId)
    .Where(b => b.BankingDate >= cutoffDate)
    .OrderByDescending(b => b.BankingDate)
    .ToListAsync();
```

### Fuel Usage List Display
```csharp
// Show fuel usage from last 14 days (not filtered by user - shared across quarry)
var cutoffDate = DateTime.Today.AddDays(-14);
var usages = await FuelUsages
    .Where(f => f.UsageDate >= cutoffDate)
    .OrderByDescending(f => f.UsageDate)
    .ToListAsync();
```

---

## Dashboard Statistics Calculation

```csharp
async Task<DashboardStats> GetDashboardStatsAsync(string userId, string quarryId)
{
    var today = DateTime.Today;
    var todayStamp = today.ToString("yyyyMMdd");

    // Get today's sales for this clerk
    var todaySales = await Sales
        .Where(s => s.ApplicationUserId == userId)
        .Where(s => s.DateStamp == todayStamp)
        .ToListAsync();

    var stats = new DashboardStats
    {
        SalesCount = todaySales.Count,
        TotalQuantity = todaySales.Sum(s => s.Quantity),
        TotalSales = todaySales.Sum(s => s.GrossSaleAmount)
    };

    // Get last sale details
    var lastSale = todaySales.OrderByDescending(s => s.DateCreated).FirstOrDefault();
    if (lastSale != null)
    {
        var product = await GetProductAsync(lastSale.ProductId);
        stats.LastSaleDescription = $"{lastSale.VehicleRegistration}: (KES {lastSale.GrossSaleAmount:N1}) " +
            $"{lastSale.Quantity:N0} pieces of {product?.ProductName} on {lastSale.SaleDate:dd/MM/yy} at {lastSale.SaleDate:hh:mm tt}";
    }

    // Get today's expenses (including auto-calculated)
    var todayExpenses = await GetExpenseItemsForReportAsync(today, today, userId, quarryId);
    stats.TotalExpenses = todayExpenses.Sum(e => e.Amount);

    // Get opening balance (previous day's closing)
    var previousDayNote = await GetDailyNoteAsync(today.AddDays(-1), quarryId);
    stats.OpeningBalance = previousDayNote?.ClosingBalance ?? 0;

    return stats;
}
```

---

## Multi-Tenant Data Isolation

**Critical Rule**: All data queries MUST filter by `QId` (quarryId) to ensure tenant isolation.

```csharp
// CORRECT - Always include quarry filter for clerk data
var sales = await context.Sales
    .Where(s => s.QId == currentUserQuarryId)  // MANDATORY
    .Where(s => s.ApplicationUserId == currentUserId)
    .ToListAsync();

// WRONG - Missing quarry filter allows cross-tenant data access
var sales = await context.Sales
    .Where(s => s.ApplicationUserId == currentUserId)
    .ToListAsync();
```

**Manager/Admin Exception:**
```csharp
// Managers can view all quarries (but still need quarry filter in reports)
if (currentUserRole == "Manager" || currentUserRole == "Administrator")
{
    // Can query without quarry filter or with specific quarry selected
    var sales = await context.Sales
        .Where(s => selectedQuarryId == null || s.QId == selectedQuarryId)
        .ToListAsync();
}
```

---

## DateStamp Convention

All date-based records use a `DateStamp` field in `yyyyMMdd` format for efficient daily grouping:

```csharp
// Setting DateStamp on save
entity.DateStamp = entity.Date.Value.ToString("yyyyMMdd");

// Querying by DateStamp
var todayRecords = await context.Sales
    .Where(s => s.DateStamp == DateTime.Today.ToString("yyyyMMdd"))
    .ToListAsync();

// Querying date range with DateStamp (more efficient than Date comparison)
var fromStamp = fromDate.ToString("yyyyMMdd");
var toStamp = toDate.ToString("yyyyMMdd");
var records = await context.Sales
    .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
    .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
    .ToListAsync();
```

---

## API Endpoints Design

### Authentication
```
POST /api/auth/login          - Login with email/password
POST /api/auth/refresh        - Refresh JWT token
POST /api/auth/logout         - Logout and invalidate token
```

### Sales
```
GET    /api/sales                     - List sales (paginated, filtered by date/quarry)
GET    /api/sales/{id}                - Get sale details with related entities
POST   /api/sales                     - Create new sale
PUT    /api/sales/{id}                - Update sale
DELETE /api/sales/{id}                - Soft delete sale

GET    /api/sales/daily-summary       - Get daily sales summary for date range
GET    /api/sales/by-product          - Sales grouped by product
```

### Expenses
```
GET    /api/expenses                  - List expenses (paginated)
GET    /api/expenses/{id}             - Get expense details
POST   /api/expenses                  - Create expense
PUT    /api/expenses/{id}             - Update expense
DELETE /api/expenses/{id}             - Soft delete

GET    /api/expenses/calculated       - Get all expenses including auto-calculated
```

### Banking
```
GET    /api/banking                   - List banking records
POST   /api/banking                   - Create banking record
PUT    /api/banking/{id}              - Update record
DELETE /api/banking/{id}              - Soft delete
```

### Fuel Usage
```
GET    /api/fuel-usage                - List fuel usage records
POST   /api/fuel-usage                - Create fuel usage record
PUT    /api/fuel-usage/{id}           - Update record
```

### Master Data
```
GET    /api/quarries                  - List quarries
GET    /api/quarries/{id}             - Get quarry details
POST   /api/quarries                  - Create quarry (Admin only)
PUT    /api/quarries/{id}             - Update quarry

GET    /api/products                  - List products
GET    /api/product-prices            - List prices for quarry

GET    /api/layers                    - List layers for quarry
POST   /api/layers                    - Create layer

GET    /api/brokers                   - List brokers for quarry
POST   /api/brokers                   - Create broker
```

### Reports
```
GET    /api/reports/sales             - Generate sales report (Excel download)
GET    /api/reports/cash-flow         - Generate cash flow statement
POST   /api/reports/send-daily        - Trigger daily report email
```

### Dashboard
```
GET    /api/dashboard/stats           - Get dashboard statistics
GET    /api/dashboard/trends          - Get sales trends data
```

---

## UI/UX Guidelines

### Design Philosophy

**Elegant Modern Design** with a mobile-first approach that prioritizes usability, aesthetics, and performance.

### Design Principles

1. **Mobile-First for Clerks**: Sales entry optimized for tablet/mobile use in field conditions
2. **Dashboard-Centric for Managers**: Rich analytics dashboard with real-time updates
3. **Minimal Clicks**: Streamlined workflows with smart defaults
4. **Always Online**: Web-based application requiring internet connectivity (no offline mode)
5. **Visual Hierarchy**: Clear typography scale, consistent spacing, purposeful color usage
6. **Responsive Breakpoints**: Fluid design that adapts gracefully across all screen sizes
7. **Touch-Friendly**: Minimum 44x44px touch targets, proper spacing for fat-finger prevention

### UI Framework: MudBlazor v8+

QDeskPro uses **MudBlazor** as the primary UI component library for its:
- Material Design 3 aesthetics
- Comprehensive component set
- Built-in responsive utilities
- Dark mode support
- Accessibility compliance

### Theme Configuration

```csharp
// Program.cs - MudBlazor Theme Setup
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.PreventDuplicates = false;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowTransitionDuration = 300;
});

// Custom Theme
public static MudTheme QDeskTheme = new MudTheme
{
    PaletteLight = new PaletteLight
    {
        Primary = "#1976D2",           // Deep Blue - primary actions
        Secondary = "#424242",          // Dark Grey - secondary elements
        Tertiary = "#4CAF50",           // Green - success states
        Info = "#2196F3",               // Light Blue - informational
        Success = "#4CAF50",            // Green - success
        Warning = "#FF9800",            // Orange - warnings
        Error = "#F44336",              // Red - errors, unpaid items
        Background = "#FAFAFA",         // Light grey background
        Surface = "#FFFFFF",            // White cards/surfaces
        AppbarBackground = "#1976D2",   // Primary blue appbar
        AppbarText = "#FFFFFF",
        DrawerBackground = "#FFFFFF",
        DrawerText = "#424242",
        DrawerIcon = "#757575",
    },
    PaletteDark = new PaletteDark
    {
        Primary = "#90CAF9",            // Light Blue for dark mode
        Secondary = "#BDBDBD",
        Tertiary = "#81C784",
        Background = "#121212",
        Surface = "#1E1E1E",
        AppbarBackground = "#1E1E1E",
    },
    Typography = new Typography
    {
        Default = new Default
        {
            FontFamily = new[] { "Inter", "Roboto", "Helvetica", "Arial", "sans-serif" },
        },
        H1 = new H1 { FontSize = "2.5rem", FontWeight = 700 },
        H2 = new H2 { FontSize = "2rem", FontWeight = 600 },
        H3 = new H3 { FontSize = "1.75rem", FontWeight = 600 },
        H4 = new H4 { FontSize = "1.5rem", FontWeight = 500 },
        H5 = new H5 { FontSize = "1.25rem", FontWeight = 500 },
        H6 = new H6 { FontSize = "1rem", FontWeight = 500 },
        Body1 = new Body1 { FontSize = "1rem", LineHeight = 1.5 },
        Body2 = new Body2 { FontSize = "0.875rem", LineHeight = 1.43 },
        Caption = new Caption { FontSize = "0.75rem" },
    },
    LayoutProperties = new LayoutProperties
    {
        DefaultBorderRadius = "8px",    // Rounded corners
        AppbarHeight = "64px",
    },
    Shadows = new Shadow
    {
        // Use subtle shadows for elevation
    },
};
```

### Mobile-First Responsive Design

```css
/* wwwroot/css/app.css - Mobile-First Breakpoints */

/* Base styles (mobile) */
.clerk-container {
    padding: 12px;
    max-width: 100%;
}

/* Small tablets (>=600px) */
@media (min-width: 600px) {
    .clerk-container {
        padding: 16px;
        max-width: 600px;
        margin: 0 auto;
    }
}

/* Tablets/Small laptops (>=960px) */
@media (min-width: 960px) {
    .clerk-container {
        padding: 24px;
        max-width: 800px;
    }
}

/* Desktop (>=1280px) */
@media (min-width: 1280px) {
    .manager-container {
        max-width: 1400px;
        margin: 0 auto;
    }
}

/* Touch-friendly targets */
.touch-target {
    min-height: 44px;
    min-width: 44px;
    padding: 12px;
}

/* Card styling */
.summary-card {
    border-radius: 12px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.08);
    transition: box-shadow 0.2s ease;
}

.summary-card:hover {
    box-shadow: 0 4px 16px rgba(0,0,0,0.12);
}

/* Highlight unpaid orders */
.unpaid-row {
    background-color: rgba(244, 67, 54, 0.08) !important;
    border-left: 3px solid #F44336;
}

/* Floating action button positioning */
.fab-container {
    position: fixed;
    bottom: 80px; /* Above bottom nav */
    right: 16px;
    z-index: 100;
}
```

### Visualizations: Chart.js Integration

QDeskPro uses **Chart.js** for all data visualizations via JavaScript interop:

```javascript
// wwwroot/js/charts.js

// Initialize Chart.js defaults
Chart.defaults.font.family = "'Inter', 'Roboto', sans-serif";
Chart.defaults.color = '#666';
Chart.defaults.plugins.tooltip.backgroundColor = 'rgba(0,0,0,0.8)';
Chart.defaults.plugins.tooltip.cornerRadius = 8;

// Sales Performance Line/Bar Chart
window.QDeskCharts = {
    salesPerformanceChart: null,
    profitGaugeChart: null,

    createSalesChart: function(canvasId, labels, salesData, expensesData) {
        const ctx = document.getElementById(canvasId).getContext('2d');

        if (this.salesPerformanceChart) {
            this.salesPerformanceChart.destroy();
        }

        this.salesPerformanceChart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Revenue',
                        data: salesData,
                        backgroundColor: 'rgba(25, 118, 210, 0.8)',
                        borderColor: '#1976D2',
                        borderWidth: 1,
                        borderRadius: 4,
                        order: 2
                    },
                    {
                        label: 'Expenses',
                        data: expensesData,
                        backgroundColor: 'rgba(244, 67, 54, 0.6)',
                        borderColor: '#F44336',
                        borderWidth: 2,
                        type: 'line',
                        fill: false,
                        tension: 0.4,
                        order: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false,
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 20
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': KES ' +
                                    context.raw.toLocaleString();
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function(value) {
                                return 'KES ' + value.toLocaleString();
                            }
                        }
                    }
                }
            }
        });
    },

    createProfitGauge: function(canvasId, profitMargin) {
        const ctx = document.getElementById(canvasId).getContext('2d');

        if (this.profitGaugeChart) {
            this.profitGaugeChart.destroy();
        }

        // Determine color based on profit margin
        let gaugeColor = '#F44336'; // Red for < 20%
        if (profitMargin >= 40) gaugeColor = '#4CAF50'; // Green
        else if (profitMargin >= 20) gaugeColor = '#FF9800'; // Orange

        this.profitGaugeChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                datasets: [{
                    data: [profitMargin, 100 - profitMargin],
                    backgroundColor: [gaugeColor, '#E0E0E0'],
                    borderWidth: 0,
                    circumference: 180,
                    rotation: 270,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '75%',
                plugins: {
                    legend: { display: false },
                    tooltip: { enabled: false }
                }
            }
        });
    },

    createProductPieChart: function(canvasId, labels, data) {
        const ctx = document.getElementById(canvasId).getContext('2d');

        return new Chart(ctx, {
            type: 'pie',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: [
                        '#1976D2', '#4CAF50', '#FF9800',
                        '#9C27B0', '#00BCD4', '#795548'
                    ],
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'right',
                        labels: { usePointStyle: true }
                    }
                }
            }
        });
    },

    destroyChart: function(chartRef) {
        if (chartRef) chartRef.destroy();
    }
};
```

### Blazor Chart Component

```razor
@* Shared/Components/SalesChart.razor *@
@inject IJSRuntime JS

<div class="chart-container" style="height: @Height">
    <canvas id="@CanvasId"></canvas>
</div>

@code {
    [Parameter] public string CanvasId { get; set; } = "salesChart";
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public List<string> Labels { get; set; } = new();
    [Parameter] public List<double> SalesData { get; set; } = new();
    [Parameter] public List<double> ExpensesData { get; set; } = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || _dataChanged)
        {
            await JS.InvokeVoidAsync("QDeskCharts.createSalesChart",
                CanvasId, Labels, SalesData, ExpensesData);
            _dataChanged = false;
        }
    }

    private bool _dataChanged = true;

    public void UpdateData(List<string> labels, List<double> sales, List<double> expenses)
    {
        Labels = labels;
        SalesData = sales;
        ExpensesData = expenses;
        _dataChanged = true;
        StateHasChanged();
    }
}
```

### App.razor - Chart.js Script Reference

```razor
<!DOCTYPE html>
<html lang="en">
<head>
    <!-- ... other head content ... -->
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet">
</head>
<body>
    <!-- ... body content ... -->

    <!-- Chart.js CDN -->
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js"></script>
    <script src="js/charts.js"></script>
</body>
</html>
```

### Page Structure

#### Clerk Views (Mobile-Optimized - based on MAUI app)

**Dashboard** (`/clerk/dashboard`)
- Current user profile with quarry assignment
- Balance Brought Forward display
- Today's summary: Quantity sold, Sales amount, Expenses amount
- Last order details
- Daily notes entry with save functionality
- Quick action buttons for common tasks

**New Sale** (`/clerk/sales/new`)
- Two-column form layout for tablet optimization
- Product picker (Size 6, Size 9, Size 4, Reject, Hardcore, Beam)
- Layer picker (Layer -1, Layer -2, etc.)
- Vehicle Registration input (required)
- Sale Date picker (limited to past 14 days)
- Expandable Client Details section:
  - Client Name
  - Phone Number
  - Destination (Kenya counties dropdown)
- Quantity and Price inputs with real-time calculation
- Broker picker with commission per unit entry
- Payment Mode picker (Cash, MPESA, Bank Transfer)
- Payment Status picker (Paid, Not Paid)
- Payment Reference text area
- **Order Summary Section**:
  - Total Amount (Quantity × Price)
  - Commission (Quantity × Commission per unit)
  - Loaders' Fee (Quantity × Quarry.LoadersFee)
  - Land Rate Fee (Quantity × Quarry.LandRateFee or RejectsFee)
  - **Net Amount** (Total - Commission - LoadersFee - LandRateFee)
- Submit Sale button with confirmation dialog

**Expenses** (`/clerk/expenses`)
- Combined entry form and list view
- Item description input
- Amount input (numeric)
- Payment Reference
- Expense Date picker
- Category picker:
  - Fuel
  - Transportation Hire
  - Maintenance and Repairs
  - Commission
  - Administrative
  - Marketing
  - Wages
  - Loaders Fees
  - Consumables and Utilities
  - Bank Charges
  - Cess and Road Fees
  - Miscellaneous
- Expandable Attachment section (photo picker/camera)
- Add/Update/Delete buttons
- Paginated expense list with tap-to-edit

**Banking** (`/clerk/banking`)
- Banking Date picker
- Amount Banked input
- Transaction Reference text area
- Expandable Attachment section
- Add/Update/Delete buttons
- Paginated banking list with tap-to-edit

**Fuel Usage** (`/clerk/fuel`)
- Old Stock input (liters B/F)
- New Stock input
- Machines Loaded input
- Wheel Loaders Loaded input
- Calculated displays:
  - Total Stock (Old + New)
  - Balance (Total - Machines - WheelLoaders)
- Usage Date picker
- Add/Update/Delete buttons
- Fuel usage history list

**Sales Report** (`/clerk/reports`)
- Date range picker (From/To)
- Get Report button
- Report display:
  - Quarry name and Clerk name
  - Sales list with unpaid orders highlighted in red
  - Expenses list
  - Fuel Usage section
  - Banking section
  - **Report Summary**:
    - Opening Balance (B/F)
    - Quantity (total pieces)
    - Sales total
    - Commissions
    - Loaders Fee
    - Land Rate (if applicable)
    - Total Expenses
    - Earnings (Sales - Expenses)
    - Unpaid Orders (highlighted if any)
    - Net Income
    - Banked amount
    - Closing Balance (Cash in Hand)
- Share button for report export

#### Manager/Admin Views (Desktop-Optimized - based on Web app)

**Analytics Dashboard** (`/dashboard`)
- Quarry selector dropdown
- Date range filter (From/To)
- Refresh Data button with loading state
- **Metric Cards**:
  - Total Revenue with average
  - Sales Orders count with daily average
  - Total Quantity sold
  - Fuel Consumption total
- **Sales Performance Chart**: Column chart showing Revenue vs Expenses trends
- **Profit Analysis**:
  - Net Income card
  - Profit Margin gauge (percentage)
  - Commission breakdown
- **Detailed Sales Summary Table**:
  - Date, Orders, Quantity, Revenue, Expenses, Net Amount columns
  - Export Data button

**Daily Sales** (`/reports/daily-sales`)
- Date range selection
- Quarry filter
- Daily breakdown with drill-down to individual sales
- Totals row

**Sale Order Details** (`/sales/{id}`)
- Full sale information with related entities (Product, Layer, Broker, Clerk)
- Edit capabilities for authorized users

**Report Generator** (`/reports/generate`)
- Custom date range selection
- Quarry selection
- Report type selection
- Excel download functionality
- Email send option

**Master Data Management**:
- Quarries (`/admin/quarries`): CRUD with fee configuration and email settings
- Products (`/admin/products`): Product type management
- Layers (`/admin/layers`): Layer management per quarry
- Brokers (`/admin/brokers`): Broker management per quarry
- Product Prices (`/admin/prices`): Price configuration per quarry/product

**User Management** (`/admin/users`):
- User list with role and quarry assignment
- Add/Edit users
- Assign users to quarries
- Role management (Administrator, Manager, Clerk)

### Component Patterns

```razor
@* Summary Card with Icon - Used for dashboard metrics *@
<MudCard Class="summary-card" Elevation="0">
    <MudCardContent Class="d-flex align-center gap-4 pa-4">
        <MudAvatar Color="Color.Primary" Size="Size.Large" Variant="Variant.Outlined">
            <MudIcon Icon="@Icons.Material.Filled.AttachMoney" />
        </MudAvatar>
        <div>
            <MudText Typo="Typo.caption" Class="text-muted">Total Revenue</MudText>
            <MudText Typo="Typo.h5" Class="font-weight-bold">KES @TotalRevenue.ToString("N0")</MudText>
            <MudText Typo="Typo.caption" Color="Color.Success">
                <MudIcon Icon="@Icons.Material.Filled.TrendingUp" Size="Size.Small" />
                +12% from yesterday
            </MudText>
        </div>
    </MudCardContent>
</MudCard>

@* Mobile-First Form Layout *@
<MudForm @ref="_form" @bind-IsValid="_isValid" @bind-Errors="_errors">
    <MudStack Spacing="3">
        @* Full width on mobile, half on tablet+ *@
        <MudGrid>
            <MudItem xs="12" sm="6">
                <MudSelect @bind-Value="_model.ProductId"
                          Label="Product"
                          Variant="Variant.Outlined"
                          AnchorOrigin="Origin.BottomCenter"
                          Required="true"
                          RequiredError="Please select a product">
                    @foreach (var product in Products)
                    {
                        <MudSelectItem Value="@product.Id">@product.ProductName</MudSelectItem>
                    }
                </MudSelect>
            </MudItem>
            <MudItem xs="12" sm="6">
                <MudNumericField @bind-Value="_model.Quantity"
                                Label="Quantity"
                                Variant="Variant.Outlined"
                                Min="1"
                                Required="true" />
            </MudItem>
        </MudGrid>

        @* Expandable Section Pattern *@
        <MudExpansionPanels Elevation="0">
            <MudExpansionPanel Text="Client Details (Optional)" MaxHeight="500">
                <MudGrid>
                    <MudItem xs="12">
                        <MudTextField @bind-Value="_model.ClientName"
                                     Label="Client Name"
                                     Variant="Variant.Outlined" />
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudTextField @bind-Value="_model.ClientPhone"
                                     Label="Phone"
                                     Variant="Variant.Outlined"
                                     InputType="InputType.Telephone" />
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudAutocomplete @bind-Value="_model.Destination"
                                        Label="Destination"
                                        SearchFunc="SearchCounties"
                                        Variant="Variant.Outlined" />
                    </MudItem>
                </MudGrid>
            </MudExpansionPanel>
        </MudExpansionPanels>

        @* Action Buttons - Full width on mobile *@
        <MudStack Row="true" Justify="Justify.FlexEnd" Class="mt-4">
            <MudButton Variant="Variant.Text" OnClick="Cancel">Cancel</MudButton>
            <MudButton Variant="Variant.Filled"
                      Color="Color.Primary"
                      Disabled="@(!_isValid || _processing)"
                      OnClick="Submit"
                      Class="touch-target">
                @if (_processing)
                {
                    <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
                }
                Save Sale
            </MudButton>
        </MudStack>
    </MudStack>
</MudForm>

@* Data Table with Mobile Responsiveness *@
<MudTable Items="@Sales"
          Hover="true"
          Breakpoint="Breakpoint.Sm"
          Dense="true"
          Loading="@_loading"
          LoadingProgressColor="Color.Primary"
          RowClass="cursor-pointer"
          OnRowClick="@(args => EditSale(args.Item))">
    <HeaderContent>
        <MudTh>Date</MudTh>
        <MudTh>Vehicle</MudTh>
        <MudTh>Product</MudTh>
        <MudTh>Qty</MudTh>
        <MudTh Style="text-align: right">Amount</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd DataLabel="Date">@context.SaleDate?.ToString("dd/MM")</MudTd>
        <MudTd DataLabel="Vehicle">@context.VehicleRegistration</MudTd>
        <MudTd DataLabel="Product">@context.Product?.ProductName</MudTd>
        <MudTd DataLabel="Qty">@context.Quantity</MudTd>
        <MudTd DataLabel="Amount" Style="text-align: right"
               Class="@(context.PaymentStatus == "NotPaid" ? "unpaid-row" : "")">
            KES @context.GrossSaleAmount.ToString("N0")
        </MudTd>
    </RowTemplate>
    <NoRecordsContent>
        <MudText Align="Align.Center" Class="pa-4">No sales recorded today</MudText>
    </NoRecordsContent>
    <PagerContent>
        <MudTablePager PageSizeOptions="new int[] { 10, 25, 50 }" />
    </PagerContent>
</MudTable>

@* Bottom Navigation for Clerks (Mobile) *@
<MudAppBar Bottom="true" Fixed="true" Color="Color.Surface" Elevation="4"
           Class="d-flex d-sm-none">
    <MudStack Row="true" Justify="Justify.SpaceAround" AlignItems="AlignItems.Center" Class="w-100">
        <MudIconButton Icon="@Icons.Material.Filled.Dashboard" Href="/clerk/dashboard" />
        <MudIconButton Icon="@Icons.Material.Filled.PointOfSale" Href="/clerk/sales" />
        <MudFab Color="Color.Primary" StartIcon="@Icons.Material.Filled.Add"
               Size="Size.Medium" OnClick="NewSale" />
        <MudIconButton Icon="@Icons.Material.Filled.Receipt" Href="/clerk/expenses" />
        <MudIconButton Icon="@Icons.Material.Filled.Assessment" Href="/clerk/reports" />
    </MudStack>
</MudAppBar>

@* Floating Action Button for New Sale *@
<div class="fab-container d-none d-sm-block">
    <MudFab Color="Color.Primary"
           StartIcon="@Icons.Material.Filled.Add"
           Size="Size.Large"
           OnClick="NewSale"
           Label="New Sale" />
</div>

@* Order Summary Card (Real-time calculations) *@
<MudPaper Class="pa-4 summary-card" Elevation="0">
    <MudText Typo="Typo.h6" Class="mb-3">Order Summary</MudText>
    <MudStack Spacing="2">
        <div class="d-flex justify-space-between">
            <MudText>Total Amount</MudText>
            <MudText Typo="Typo.body1" Class="font-weight-medium">KES @TotalAmount.ToString("N0")</MudText>
        </div>
        <div class="d-flex justify-space-between">
            <MudText Color="Color.Secondary">Commission</MudText>
            <MudText Color="Color.Secondary">- KES @Commission.ToString("N0")</MudText>
        </div>
        <div class="d-flex justify-space-between">
            <MudText Color="Color.Secondary">Loaders Fee</MudText>
            <MudText Color="Color.Secondary">- KES @LoadersFee.ToString("N0")</MudText>
        </div>
        @if (LandRateFee > 0)
        {
            <div class="d-flex justify-space-between">
                <MudText Color="Color.Secondary">Land Rate</MudText>
                <MudText Color="Color.Secondary">- KES @LandRateFee.ToString("N0")</MudText>
            </div>
        }
        <MudDivider Class="my-2" />
        <div class="d-flex justify-space-between">
            <MudText Typo="Typo.h6">Net Amount</MudText>
            <MudText Typo="Typo.h6" Color="Color.Primary" Class="font-weight-bold">
                KES @NetAmount.ToString("N0")
            </MudText>
        </div>
    </MudStack>
</MudPaper>
```

---

## Data Access Patterns

### Entity Framework Configuration

```csharp
// Use shadow properties for audit fields
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.DateCreated = DateTime.UtcNow;
                    entry.Entity.IsActive = true;
                    break;
                case EntityState.Modified:
                    entry.Entity.DateModified = DateTime.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
```

### Query Patterns

```csharp
// Always filter by QuarryId for multi-tenant queries
public async Task<List<Sale>> GetSalesByDateAsync(string quarryId, DateTime from, DateTime to)
{
    return await _context.Sales
        .Where(s => s.QId == quarryId)
        .Where(s => s.SaleDate >= from && s.SaleDate <= to)
        .Include(s => s.Product)
        .Include(s => s.Layer)
        .Include(s => s.Broker)
        .OrderByDescending(s => s.SaleDate)
        .ToListAsync();
}
```

---

## Improvements Over Legacy QDesk

### Architecture Improvements
1. **Unified Project**: Single deployable unit instead of separate API + Web + Shared projects
2. **Feature-Based Organization**: Code organized by feature instead of layer
3. **Minimal APIs**: Lighter, faster API endpoints
4. **Better Separation**: Clear boundaries between UI, business logic, and data access

### UX Improvements
1. **Responsive Design**: Works seamlessly on mobile, tablet, and desktop
2. **Real-time Updates**: SignalR for live dashboard without page refresh
3. **Faster Data Entry**: Keyboard shortcuts, smart defaults, auto-complete
4. **Better Error Handling**: Clear validation messages, graceful error recovery
5. **Improved Search**: Full-text search across sales, with filters

### Performance Improvements
1. **Efficient Queries**: Optimized EF Core queries with proper indexing
2. **Caching**: Redis/in-memory cache for master data
3. **Pagination**: Server-side pagination for large datasets
4. **Lazy Loading**: Components load data on demand

### Business Logic Improvements
1. **Centralized Calculations**: All fee calculations in domain services
2. **Audit Trail**: Complete history of changes
3. **Soft Deletes**: No data loss, recoverable deletes
4. **Validation Pipeline**: Consistent validation across API and UI

---

## Security Considerations

### Authentication
- JWT tokens with refresh token rotation
- Secure password hashing with ASP.NET Core Identity
- Account lockout after failed attempts

### Authorization
- Role-based access control (Admin, Manager, Clerk)
- Quarry-level data isolation (clerks only see assigned quarry)
- API endpoint authorization with policies

### Data Protection
- HTTPS enforced
- SQL injection prevention via EF Core parameterized queries
- XSS prevention via Blazor's built-in encoding
- CSRF protection for form submissions

---

## Testing Strategy

### Unit Tests
- Domain services (calculation logic)
- Validation rules
- Entity behavior

### Integration Tests
- API endpoints
- Database operations
- Authentication flows

### UI Tests
- Critical user flows (sale entry, report generation)
- Form validation
- Navigation

---

## Deployment

### Requirements
- .NET 10 Runtime
- SQL Server 2019+ or Azure SQL
- HTTPS certificate
- SMTP server for email reports

### Configuration
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=QDeskPro;..."
  },
  "JwtSettings": {
    "Key": "...",
    "Issuer": "QDeskPro",
    "Audience": "QDeskProUsers",
    "DurationInMinutes": 60
  },
  "EmailSettings": {
    "SmtpServer": "...",
    "Port": 587,
    "Username": "...",
    "Password": "..."
  }
}
```

---

## Development Guidelines

### Code Style
- Use C# 13 features (primary constructors, collection expressions)
- Prefer `record` types for DTOs
- Use `required` modifier for mandatory properties
- Follow .NET naming conventions

### Git Workflow
- Feature branches from `main`
- Conventional commits (feat:, fix:, docs:, etc.)
- PR reviews required

### Commit Message Format
```
feat(sales): add bulk sale import functionality

- Added CSV import endpoint
- Implemented validation for imported data
- Added progress tracking for large imports
```

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Program.cs` | Application configuration, DI setup, middleware pipeline |
| `Data/AppDbContext.cs` | EF Core DbContext with all DbSets and configurations |
| `Domain/Entities/*.cs` | Domain entity classes |
| `Domain/Services/*.cs` | Business logic services |
| `Features/*/Pages/*.razor` | Blazor page components |
| `Features/*/Services/*.cs` | Feature-specific services |
| `Api/Endpoints/*.cs` | Minimal API endpoint definitions |
| `Shared/Layouts/MainLayout.razor` | Main application layout |

---

## Common Tasks

### Adding a New Feature
1. Create feature folder under `Features/`
2. Add entity in `Domain/Entities/` if needed
3. Add DbSet in `AppDbContext`
4. Create migration
5. Add service in `Domain/Services/` or `Features/*/Services/`
6. Add API endpoints in `Api/Endpoints/`
7. Add Blazor pages in `Features/*/Pages/`
8. Update navigation menu

### Adding a New Report
1. Add method in `Features/Reports/Services/ReportService.cs`
2. Create Excel generation logic using ClosedXML
3. Add API endpoint for download
4. Add UI page/button for triggering

### Modifying Business Calculations
1. Update domain service (e.g., `SalesCalculationService`)
2. Ensure consistency between API and UI calculations
3. Add unit tests for new calculation logic
4. Update documentation in this file if rules change

---

## Detailed Business Calculations

### Sale Calculation Formulas

```csharp
// Per-sale calculations
GrossSaleAmount = Quantity × PricePerUnit;
TotalCommission = Quantity × CommissionPerUnit;
TotalLoadersFee = Quantity × Quarry.LoadersFee;

// Land Rate depends on product type
if (Product.ProductName == "Reject")
    TotalLandRateFee = Quantity × Quarry.RejectsFee;
else
    TotalLandRateFee = Quantity × Quarry.LandRateFee;

// Net amount for the quarry
NetSaleAmount = GrossSaleAmount - TotalCommission - TotalLoadersFee - TotalLandRateFee;
```

### Fuel Usage Calculation

```csharp
TotalStock = OldStock + NewStock;
Used = MachinesLoaded + WheelLoadersLoaded;
Balance = TotalStock - Used;
```

### Daily Summary Calculation

```csharp
// For a given date range
TotalQuantity = Sum(Sales.Quantity);
TotalSalesAmount = Sum(Sales.GrossSaleAmount);
TotalCommission = Sum(Sales.TotalCommission);
TotalLoadersFee = Sum(Sales.TotalLoadersFee);
TotalLandRateFee = Sum(Sales.TotalLandRateFee);
TotalManualExpenses = Sum(Expenses.Amount);

TotalExpenses = TotalManualExpenses + TotalCommission + TotalLoadersFee + TotalLandRateFee;
NetEarnings = TotalSalesAmount - TotalExpenses;
TotalBanked = Sum(Banking.AmountBanked);
CashInHand = OpeningBalance + NetEarnings - TotalBanked;
```

### Report Summary Fields

| Field | Calculation |
|-------|-------------|
| Opening Balance (B/F) | Previous day's DailyNote.ClosingBalance (only for single-day reports) |
| Quantity | Total pieces sold |
| Sales | Sum of GrossSaleAmount |
| Commissions | Sum of TotalCommission from sales |
| Loaders Fee | Sum of TotalLoadersFee from sales |
| Land Rate | Sum of TotalLandRateFee from sales (hidden if Quarry.LandRateFee = 0) |
| Total Expenses | Manual Expenses + Commissions + Loaders Fee + Land Rate |
| Earnings | Sales - Total Expenses |
| Unpaid Orders | Sum of GrossSaleAmount where PaymentStatus = NotPaid |
| Net Income | (Earnings + OpeningBalance) - UnpaidOrders |
| Banked | Sum of AmountBanked |
| Closing Balance | Net Income - Banked |

---

## Critical: Sales Report Expense Calculation Logic

**THIS IS CRUCIAL FOR ACCURATE REPORTS**

When generating expense items for a report, the system must generate expense line items from FOUR sources:

### 1. User Expenses (Manual Entries)
```csharp
// Query expenses entered by user within date range
var userExpenses = Expenses
    .Where(x => x.ExpenseDate >= fromDate && x.ExpenseDate <= toDate)
    .Where(x => x.QId == quarryId)
    .ToList();

foreach (var item in userExpenses)
{
    yield return new SaleReportLineItem
    {
        ItemDate = item.ExpenseDate,
        LineItem = item.Item,
        Amount = item.Amount,
        LineType = "User Expense"  // IMPORTANT: This tag is used for filtering
    };
}
```

### 2. Commission Expenses (Auto-generated from Sales)
```csharp
// For each sale with CommissionPerUnit > 0
var commissionSales = Sales
    .Where(x => x.CommissionPerUnit > 0)
    .Where(x => x.SaleDate >= fromDate && x.SaleDate <= toDate)
    .ToList();

foreach (var sale in commissionSales)
{
    var product = GetProduct(sale.ProductId);
    var broker = sale.BrokerId != null ? GetBroker(sale.BrokerId) : null;

    yield return new SaleReportLineItem
    {
        ItemDate = sale.SaleDate,
        LineItem = $"{sale.VehicleRegistration} | {product.ProductName} - {sale.Quantity:N0} pieces sale commission"
                 + (broker != null ? $" to {broker.BrokerName}" : ""),
        Amount = sale.Quantity * sale.CommissionPerUnit,
        LineType = "Commission Expense"  // IMPORTANT: Used for sum calculation
    };
}
```

### 3. Loaders Fee Expenses (Auto-generated from Sales)
```csharp
// For each sale, if quarry has LoadersFee set
var quarry = GetQuarry(quarryId);
if (quarry.LoadersFee > 0)
{
    foreach (var sale in allSalesInRange)
    {
        yield return new SaleReportLineItem
        {
            ItemDate = sale.SaleDate,
            LineItem = $"{sale.VehicleRegistration} loaders fee for {sale.Quantity:N0} pieces",
            Amount = sale.Quantity * quarry.LoadersFee,
            LineType = "Loaders Fee Expense"  // IMPORTANT
        };
    }
}
```

### 4. Land Rate Fee Expenses (Auto-generated from Sales)
```csharp
// For each sale, if quarry has LandRateFee set
if (quarry.LandRateFee > 0)
{
    foreach (var sale in allSalesInRange)
    {
        var product = GetProduct(sale.ProductId);
        double feeRate;

        // SPECIAL CASE: Reject products use RejectsFee instead
        if (product.ProductName.ToLower().Contains("reject"))
        {
            feeRate = quarry.RejectsFee ?? 0;
        }
        else
        {
            feeRate = quarry.LandRateFee;
        }

        yield return new SaleReportLineItem
        {
            ItemDate = sale.SaleDate,
            LineItem = $"{sale.VehicleRegistration} land rate fee for {sale.Quantity:N0} pieces",
            Amount = sale.Quantity * feeRate,
            LineType = "Land Rate Fee Expense"  // IMPORTANT
        };
    }
}
```

### Expense Summary Calculation

```csharp
// After collecting ALL expense items from all 4 sources:
var allExpenses = userExpenses
    .Concat(commissionExpenses)
    .Concat(loadersFeeExpenses)
    .Concat(landRateExpenses)
    .OrderBy(x => x.ItemDate)
    .ToList();

// Calculate category totals by LineType
TotalExpenses = allExpenses.Sum(x => x.Amount);
Commission = allExpenses.Where(x => x.LineType == "Commission Expense").Sum(x => x.Amount);
LoadersFee = allExpenses.Where(x => x.LineType == "Loaders Fee Expense").Sum(x => x.Amount);
LandRateFee = allExpenses.Where(x => x.LineType == "Land Rate Fee Expense").Sum(x => x.Amount);

// Calculate report summary
Earnings = TotalSales - TotalExpenses;
NetEarnings = (Earnings + OpeningBalance) - Unpaid;  // Unpaid = sales with PaymentStatus != "Paid"
CashInHand = NetEarnings - Banked;
```

### Opening Balance Logic

```csharp
// Only calculate opening balance for SINGLE-DAY reports
if (fromDate.Date == toDate.Date)
{
    // Get previous day's closing balance from DailyNote
    var previousDayNote = await GetDailyNote(fromDate.AddDays(-1), quarryId);
    OpeningBalance = previousDayNote?.ClosingBalance ?? 0;
}
else
{
    // Multi-day reports don't show opening balance
    OpeningBalance = 0;
}
```

### Saving Daily Closing Balance

```csharp
// After generating single-day report, save closing balance to DailyNote
if (fromDate.Date == toDate.Date)
{
    var note = await GetOrCreateDailyNote(toDate, quarryId);
    note.ClosingBalance = CashInHand;  // This becomes next day's opening balance
    await SaveDailyNote(note);
}
```

### SaleReportLineItem Model

```csharp
public class SaleReportLineItem
{
    public DateOnly ItemDate { get; set; }
    public string LineItem { get; set; }      // Description
    public string Product { get; set; }        // Product name (for sales)
    public double Quantity { get; set; }       // Quantity sold (for sales)
    public double Amount { get; set; }
    public bool Paid { get; set; }             // Payment status (for sales)
    public string LineType { get; set; }       // "User Expense", "Commission Expense",
                                               // "Loaders Fee Expense", "Land Rate Fee Expense"
}
```

### Report Display Order

In the shared/exported report:
1. **Sales Section**
   - Grouped by product for summary: `{ProductName} {TotalQty}pcs: {TotalAmount}`
   - Or individual lines: `{VehicleReg} - {Product}: {Qty} * {Price}`

2. **Expenses Section** (in this order)
   - Commissions total
   - Loaders Fee total
   - Land Rate total (only if LandRateFee > 0)
   - User expenses (individual items)

3. **Fuel Usage Section**
   - Old Stock | New Stock | Machines | W/Loaders | Balance

4. **Banking Section**
   - Individual banking items with reference
   - Total Banked

5. **Summary Section**
   - Opening Balance
   - Quantity (pieces)
   - Sales
   - Total Expenses
   - Earnings
   - Unpaid Orders
   - Net Income
   - Banked
   - Closing Balance
   - Daily Notes (if any)

---

## Reference Data

### Kenya Counties (for Destination dropdown)

```csharp
public static readonly string[] KenyaCounties =
[
    "Baringo", "Bomet", "Bungoma", "Busia", "Elgeyo-Marakwet",
    "Embu", "Garissa", "Homa Bay", "Isiolo", "Kajiado",
    "Kakamega", "Kericho", "Kiambu", "Kilifi", "Kirinyaga",
    "Kisii", "Kisumu", "Kitui", "Kwale", "Laikipia",
    "Lamu", "Machakos", "Makueni", "Mandera", "Marsabit",
    "Meru", "Migori", "Mombasa", "Murang'a", "Nairobi",
    "Nakuru", "Nandi", "Narok", "Nyamira", "Nyandarua",
    "Nyeri", "Samburu", "Siaya", "Taita-Taveta", "Tana River",
    "Tharaka-Nithi", "Trans-Nzoia", "Turkana", "Uasin Gishu",
    "Vihiga", "Wajir", "West Pokot"
];
```

### Default Products

```csharp
public static readonly string[] DefaultProducts =
[
    "Size 6",      // Standard ballast size
    "Size 9",      // Medium ballast
    "Size 4",      // Smaller ballast
    "Reject",      // Rejected/irregular pieces (different fee structure)
    "Hardcore",    // Larger building material
    "Beam"         // Structural pieces
];
```

### Expense Categories

```csharp
public static readonly string[] ExpenseCategories =
[
    "Fuel",
    "Transportation Hire",
    "Maintenance and Repairs",
    "Commission",
    "Administrative",
    "Marketing",
    "Wages",
    "Loaders Fees",
    "Consumables and Utilities",
    "Bank Charges",
    "Cess and Road Fees",
    "Miscellaneous"
];
```

### Payment Modes

```csharp
public enum PaymentMode
{
    Cash,
    MPESA,      // Mobile money (Kenya)
    BankTransfer
}
```

### Payment Status

```csharp
public enum PaymentStatus
{
    Paid,
    NotPaid     // Credit sales tracked for collection
}
```

---

## Database Seed Data

### Initial Quarries
```csharp
new Quarry { QuarryName = "Thika - Komu", Location = "Thika", LoadersFee = 50, LandRateFee = 10, RejectsFee = 5 },
new Quarry { QuarryName = "Nyahururu", Location = "Nyahururu", LoadersFee = 50, LandRateFee = 10, RejectsFee = 5 }
```

### Initial Roles
```csharp
new IdentityRole { Name = "Administrator" },
new IdentityRole { Name = "Manager" },
new IdentityRole { Name = "Clerk" }
```

### Default Users (Development)
```csharp
new ApplicationUser { Email = "admin@localhost.com", FullName = "System Admin", Role = "Administrator" },
new ApplicationUser { Email = "clerk@localhost.com", FullName = "Test Clerk", Role = "Clerk" }
```

---

## Error Handling Patterns

### Validation Errors
- Return `400 Bad Request` with structured error messages
- Client-side validation mirrors server-side rules
- Form shows field-specific error messages

### Business Logic Errors
- Return `422 Unprocessable Entity` for business rule violations
- Example: "Cannot backdate sale more than 14 days"

### Not Found
- Return `404 Not Found` for missing resources
- Check user's quarry access before returning 403

### Authorization Errors
- Return `403 Forbidden` when user lacks permissions
- Example: Clerk trying to access another quarry's data

### Server Errors
- Return `500 Internal Server Error` with correlation ID
- Log full exception details server-side
- Show user-friendly message on client

---

## Performance Optimization Targets

### Page Load Times
- Dashboard: < 500ms initial render
- Sales List: < 300ms with pagination
- Report Generation: < 2s for 30-day report

### Data Entry
- Sale form submission: < 1s response
- Real-time calculation: < 100ms on input change

### Caching Strategy
- Master data (Products, Layers, Brokers): 5-minute cache
- User's quarry data: Session-scoped
- Dashboard stats: 1-minute cache with SignalR invalidation

---

## AI Integration

QDeskPro includes AI-powered features to enhance sales processing, analytics, and reporting through natural language queries. *Note: This is planned for a future phase.*

---

## Key Files Reference (Simplified)

| File | Purpose |
|------|---------|
| `Program.cs` | Application configuration, DI setup, middleware pipeline |
| `Data/AppDbContext.cs` | EF Core DbContext with all DbSets and configurations |
| `Domain/Entities/*.cs` | Domain entity classes |
| `Domain/Services/*.cs` | Business logic services |
| `Features/*/Pages/*.razor` | Blazor page components |
| `Components/Layout/MainLayout.razor` | Main application layout |
| `Components/Routes.razor` | Router and theme configuration |

---

## Summary

This is a simplified Blazor Server application without PWA, WebAssembly, or offline capabilities. It provides:
- Server-side rendering with interactive components via SignalR
- Cookie-based authentication with ASP.NET Core Identity
- Real-time updates for dashboard data
- Mobile-responsive design for field operations
- Excel report generation and email delivery
