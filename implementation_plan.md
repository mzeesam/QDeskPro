# QDeskPro Implementation Plan

## Overview

This document outlines the implementation plan for QDeskPro, a modern unified Blazor web application that replaces the legacy QDesk system (MAUI + Web + API). The new application will be built on .NET 10 with a single unified project structure.

---

## Phase 1: Project Setup & Foundation

### 1.1 Create Solution Structure

**Tasks:**
- [ ] Create `QDeskPro.sln` solution file
- [ ] Create unified Blazor Web App project with Interactive Server + WebAssembly rendering
- [ ] Configure project for .NET 10
- [ ] Add NuGet packages:
  - `MudBlazor` (v8+) - UI components with Material Design 3
  - `Microsoft.EntityFrameworkCore.SqlServer` - Database
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore` - Authentication
  - `ClosedXML` - Excel export
  - `Serilog` - Logging
  - `WebPush` - Push notifications
- [ ] Set up folder structure as defined in claude.md
- [ ] Configure `appsettings.json` with connection strings, JWT settings, email config, VAPID keys
- [ ] Add Chart.js CDN reference in App.razor for visualizations
- [ ] Create custom MudBlazor theme with QDesk brand colors (see claude.md Theme Configuration)
- [ ] Add Inter font from Google Fonts for modern typography

**Deliverables:**
- Empty but runnable Blazor application
- Project compiles and runs with MudBlazor template

---

### 1.2 Database Layer Setup

**Tasks:**
- [ ] Create `Domain/Entities/BaseEntity.cs` with common audit fields
- [ ] Create all domain entities:
  - `ApplicationUser` (extends IdentityUser)
  - `Quarry`
  - `Layer`
  - `Product`
  - `ProductPrice`
  - `Broker`
  - `Sale`
  - `Expense`
  - `Banking`
  - `FuelUsage`
  - `DailyNote`
  - `UserQuarry`
- [ ] Create `Data/AppDbContext.cs` with:
  - All DbSets
  - Entity configurations
  - SaveChangesAsync override for audit fields
- [ ] Create initial migration
- [ ] Create `Data/Seed/` classes for default data:
  - Default roles (Administrator, Manager, Clerk)
  - Default products (Size 6, Size 9, Size 4, Reject, Hardcore, Beam)
  - Default Administrator user (for first login)
  - Sample manager, quarry, and clerk (development only)

**Deliverables:**
- Complete database schema matching legacy system
- Seed data applied on first run

---

### 1.3 Authentication & Authorization Setup

**Tasks:**
- [ ] Configure ASP.NET Core Identity
- [ ] Set up cookie authentication for Blazor Server
- [ ] Create login/logout pages (`Features/Auth/Pages/`)
- [ ] Implement `AuthenticationStateProvider` for Blazor
- [ ] Create authorization policies:
  - `RequireAdministrator`
  - `RequireManagerOrAdmin`
  - `RequireClerk`
  - `RequireQuarryAccess` (custom handler for quarry-level access)
- [ ] Create `QuarryAccessHandler` for resource-based authorization
- [ ] Create role-based navigation filtering

**User Role Hierarchy (see claude.md for full details):**

```
Administrator
    └── Creates/Manages → Managers only (cannot create quarries or clerks)

Manager (can own MULTIPLE quarries - one-to-many relationship)
    └── Creates/Manages → Own Quarries (unlimited, via Quarry.ManagerId)
    └── Creates/Manages → Clerks (for own quarries)
    └── Assigns → Users to own quarries
    └── Views → Analytics & Reports (own quarries)

Clerk
    └── Captures → Daily operations (assigned quarry only)
    └── Views → Sales Reports (own data only)
```

**Key Ownership Model:**
- When Administrator creates a Manager, that Manager can then create and own multiple quarries
- Each Quarry has `ManagerId` foreign key pointing to the owning Manager
- A Manager's `OwnedQuarries` collection contains all quarries they created/own

**Access Control Summary:**
| Action | Administrator | Manager | Clerk |
|--------|--------------|---------|-------|
| Create Managers | ✅ | ❌ | ❌ |
| Create Quarries | ❌ | ✅ | ❌ |
| Create Clerks | ❌ | ✅ (own quarries) | ❌ |
| Manage Master Data | ❌ | ✅ (own quarries) | ❌ |
| Capture Sales/Expenses | ❌ | ❌ | ✅ |
| View Analytics | ✅ | ✅ (own quarries) | ❌ |
| Generate Reports | ✅ | ✅ (own quarries) | ✅ (own data) |

**Deliverables:**
- Working login/logout flow
- Role-based page access
- Quarry-level authorization
- User context available throughout app

---

### 1.4 PWA Foundation Setup

**Tasks:**
- [ ] Create `wwwroot/manifest.json` with app metadata:
  - App name, short name, description
  - Theme color (#1976D2) and background color
  - Display mode (standalone)
  - Icons (192x192, 512x512, maskable)
  - Shortcuts for quick actions (New Sale, Report)
  - Screenshots for install prompt
- [ ] Create `wwwroot/service-worker.js` with caching strategies:
  - Cache-first for static assets (CSS, JS, fonts)
  - Network-first for API calls with offline fallback
  - Stale-while-revalidate for images
- [ ] Create `wwwroot/service-worker.published.js` for production
- [ ] Add PWA meta tags to `App.razor`:
  - Manifest link
  - Theme color meta tag
  - Apple touch icon
  - Apple mobile web app capable
- [ ] Register service worker in `App.razor`
- [ ] Create app icons in `wwwroot/icons/`:
  - icon-192x192.png
  - icon-512x512.png
  - icon-maskable-192x192.png
  - icon-maskable-512x512.png
- [ ] Add offline fallback page (`wwwroot/offline.html`)

**Deliverables:**
- Installable PWA with app icons
- Offline capability for static content
- Add to home screen functionality

---

## Phase 2: Core Features - Clerk Operations

### 2.1 Clerk Dashboard

**Tasks:**
- [ ] Create `Features/Dashboard/Pages/ClerkDashboard.razor`
- [ ] Create `Features/Dashboard/Services/DashboardService.cs`
- [ ] Implement:
  - User profile display with quarry name
  - Today's summary cards (Quantity, Sales, Expenses)
  - Opening balance display
  - Last order info
  - Daily notes entry with save
  - Quick action buttons (New Sale, Expenses, Banking, Fuel)
- [ ] Mobile-responsive layout

**UI Logic from Legacy MAUI App:**

**Dashboard Components:**
| Component | Description |
|-----------|-------------|
| User Profile | FullName, Position, Quarry Name |
| Balance B/F | Opening balance from previous day's DailyNote.ClosingBalance |
| Sales Qty | Sum of today's sales quantities |
| Sales Amount | Sum of today's sales amounts |
| Expenses | Sum of today's expenses |
| Last Order | Most recent sale details |
| Daily Notes | Expandable editor for today's notes |

**Daily Notes Logic:**

**Form Fields:**
| Field | Type | Constraints |
|-------|------|-------------|
| Notes | MudTextField (multiline) | Optional; free-text notes for the day |

**Auto-Loading (on page load):**
```
note = GetTodayNoteAsync(DateTime.Today.ToString("yyyyMMdd"))

if (note == null):
    note = new DailyNote {
        quarryId = CurrentUser.QuarryId,
        NoteDate = DateTime.Now,
        DateStamp = DateTime.Today.ToString("yyyyMMdd"),
        QId = CurrentUser.QuarryId,
        ClosingBalance = 0,
        Notes = ""
    }
    SaveDailyNoteAsync(note)  // Auto-create for today
```

**Save Notes Command:**
```
if (!string.IsNullOrWhiteSpace(Note.Notes)):
    SaveDailyNoteAsync(Note)
    ShowToast("Daily note has been saved/updated...")
```

**Special UI Behaviors:**
- Daily Notes in collapsible MudExpansionPanel (initially collapsed)
- Inline save button (icon) next to editor
- Notes persist across app sessions via DailyNote entity
- ClosingBalance field updated automatically by Sales Report generation
- DateStamp links note to specific date (format: "yyyyMMdd")

**Quick Action Buttons:**
- New Sale → Navigate to `/clerk/sales/new`
- Expenses → Navigate to `/clerk/expenses`
- Banking → Navigate to `/clerk/banking`
- Fuel Usage → Navigate to `/clerk/fuel`
- Report → Navigate to `/clerk/reports`

**Summary Cards Data Queries:**
```
// Today's stats filtered by DateStamp and QuarryId
TodayQuantity = Sum(Sales.Quantity WHERE DateStamp == Today)
TodaySales = Sum(Sales.GrossAmount WHERE DateStamp == Today)
TodayExpenses = Sum(Expenses.Amount WHERE DateStamp == Today)
LastOrder = GetMostRecentSale(QuarryId)
```

**Deliverables:**
- Functional clerk dashboard with real-time data
- Mobile-optimized design

---

### 2.2 Sales Entry

**Tasks:**
- [ ] Create `Features/Sales/Pages/NewSale.razor`
- [ ] Create `Features/Sales/Components/`:
  - `SaleForm.razor` - Main form component
  - `OrderSummary.razor` - Calculation display
  - `ProductPicker.razor` - Product selection
  - `LayerPicker.razor` - Layer selection
  - `BrokerPicker.razor` - Broker selection with commission
  - `ClientDetails.razor` - Expandable client info
  - `PaymentDetails.razor` - Payment mode/status/reference
- [ ] Create `Features/Sales/Services/SaleService.cs`:
  - `CreateSaleAsync()`
  - `GetSalesForClerkAsync()`
  - `UpdateSaleAsync()`
  - `DeleteSaleAsync()`
- [ ] Create `Domain/Services/SaleCalculationService.cs`:
  - `CalculateGrossAmount()`
  - `CalculateCommission()`
  - `CalculateLoadersFee()`
  - `CalculateLandRateFee()`
  - `CalculateNetAmount()`
- [ ] Implement real-time calculation as user types
- [ ] Implement Kenya counties dropdown
- [ ] Add validation (required fields, date limits, positive values)
- [ ] Create `Features/Sales/Pages/MySales.razor` - List view with edit

**UI Logic from Legacy MAUI App:**

**Form Fields:**
| Field | Type | Constraints |
|-------|------|-------------|
| Product | MudSelect | Required; auto-populates Price from ProductPrice table |
| Layer | MudSelect | Required; filtered by quarry |
| Vehicle Registration | MudTextField | Required; max 90 chars |
| Sale Date | MudDatePicker | Required; Min: Today-14 days, Max: Today |
| Client Name | MudTextField | Optional; in expandable section |
| Client Phone | MudTextField | Optional; in expandable section |
| Destination | MudAutocomplete | Optional; Kenya counties (47 counties) |
| Quantity | MudNumericField | Required; triggers calculations on change |
| Price | MudNumericField | Required; auto-populated from product; triggers calculations |
| Broker | MudSelect | Optional; defaults to first broker |
| Commission/Unit | MudNumericField | Optional; triggers calculations |
| Payment Mode | MudSelect | Options: Cash, MPESA, Bank Transfer; Default: MPESA |
| Payment Status | MudSelect | Options: Paid, Not Paid; Default: Paid |
| Payment Reference | MudTextField | Optional |

**Default Values:**
- `SaleDate = DateTime.Now`
- `PaymentMode = "MPESA"`
- `PaymentStatus = "Paid"`
- `Price` auto-set when Product selected (from ProductPrice for quarry)
- `Broker` defaults to first in list

**Real-time Calculations (on Quantity/Price/Commission change):**
```
GrossAmount = Quantity × Price
Commission = Quantity × CommissionPerUnit
LoadersFee = Quantity × Quarry.LoadersFee
LandRateFee = Quantity × Quarry.LandRateFee (if > 0)
  // Special: If product contains "reject" AND Quarry.RejectsFee > 0:
  LandRateFee = Quantity × Quarry.RejectsFee
NetAmount = GrossAmount - Commission - LoadersFee - LandRateFee
  // NetAmount minimum is 0 (no negatives)
```

**Validation Rules:**
1. Layer and Product: Required - "Please specify layer/product"
2. Quantity and Price: Must be numeric and > 0 - "Cannot proceed if price/quantity not provided"
3. Vehicle Registration: Required, not empty - "Please provide the quantity/registration details"
4. Confirmation dialog before save: "Submit '{VehicleReg} - {Product}' {Quantity:N0} pieces?"

**Data Manipulation on Save:**
1. Set `Sale.PricePerUnit = Price` (from input)
2. Set `Sale.ProductId = Product.Id`
3. Set `Sale.LayerId = Layer.Id`
4. Set `Sale.BrokerId = Broker?.Id`
5. Create `DateStamp = SaleDate.ToString("yyyyMMdd")`
6. Set `ApplicationUserId = CurrentUser.UserId`
7. Set `QId = CurrentUser.QuarryId`
8. After save: Reset form, navigate to Dashboard

**Special UI Behaviors:**
- Client Details in collapsible MudExpansionPanel (initially collapsed)
- Clear "0" from Price/Quantity fields when focused
- Activity indicator overlay during save
- Order Summary section shows all calculated values with Net Amount bold/underlined
- LandRateFee row conditionally visible based on Quarry.LandRateFee > 0

**Deliverables:**
- Complete sale entry form with auto-calculations
- Sale list with edit/delete capabilities
- All calculations matching legacy system

---

### 2.3 Expenses Management

**Tasks:**
- [ ] Create `Features/Expenses/Pages/Expenses.razor`
- [ ] Create `Features/Expenses/Components/ExpenseForm.razor`
- [ ] Create `Features/Expenses/Services/ExpenseService.cs`:
  - `CreateExpenseAsync()`
  - `GetExpensesForClerkAsync()`
  - `UpdateExpenseAsync()`
  - `DeleteExpenseAsync()`
- [ ] Implement expense categories dropdown
- [ ] Add file attachment support (optional enhancement)
- [ ] Create paginated list with tap-to-edit
- [ ] Add validation

**UI Logic from Legacy MAUI App:**

**Form Fields:**
| Field | Type | Constraints |
|-------|------|-------------|
| Item (Description) | MudTextField | Required; expense description |
| Amount | MudNumericField | Required; must be > 0 |
| Payment Reference | MudTextField (multiline) | Optional; transaction reference |
| Expense Date | MudDatePicker | Required; Min: Today-14 days, Max: Today |
| Category | MudSelect | Required; 12 predefined categories |
| Attachment | File Upload | Optional; photos (max 10 images) |

**Expense Categories (Fixed List):**
1. Fuel
2. Transportation Hire
3. Maintenance and Repairs
4. Commission
5. Administrative
6. Marketing
7. Wages
8. Loaders Fees
9. Consumables and Utilities
10. Bank Charges
11. Cess and Road Fees
12. Miscellaneous

**Default Values:**
- `ExpenseDate = DateTime.Now`
- `ButtonText = "Save"` (changes to "Update" in edit mode)
- `IsCancelVisible = false` (shows Delete/Cancel only in edit mode)

**Validation Rules:**
1. Item: Required, not empty
2. Amount: Must be > 0
3. Error message: "Please provide the description/amount details..."

**Data Manipulation on Save:**
1. If Attachment exists:
   - Convert file to bytes
   - Base64 encode: `Expense.Base64FileData = Convert.ToBase64String(bytes)`
   - Set MIME type: `Expense.ContentType = GetMimeType(extension)`
2. Create `DateStamp = ExpenseDate.ToString("yyyyMMdd")`
3. If new expense: Set `QId = CurrentUser.QuarryId`
4. Save to database
5. Show toast: "New expense has been captured!" or "Expense has been updated!"
6. Reset form and refresh expense list

**Edit Mode Activation:**
- On tap/click of expense item in list:
  - Load expense into form
  - Set `ButtonText = "Update"`
  - Set `IsCancelVisible = true` (show Delete/Cancel buttons)

**Special UI Behaviors:**
- Combined form and list view on same page
- Attachment section in collapsible MudExpansionPanel (initially collapsed)
- Photo picker supports multiple photos (up to 10)
- Thumbnail previews (100x100px) with removable labels
- Pagination: Load 12 items initially, infinite scroll for more
- Expense list sorted by ExpenseDate descending (newest first)
- Each list item shows: Icon + Description, Amount (bold, primary), Reference, Date/Time
- Delete confirmation dialog before removal

**Deliverables:**
- Complete expense entry with categories
- Expense list with edit/delete

---

### 2.4 Banking Records

**Tasks:**
- [ ] Create `Features/Banking/Pages/Banking.razor`
- [ ] Create `Features/Banking/Components/BankingForm.razor`
- [ ] Create `Features/Banking/Services/BankingService.cs`
- [ ] Implement CRUD operations
- [ ] Add validation
- [ ] Create paginated list with edit

**UI Logic from Legacy MAUI App:**

**Form Fields:**
| Field | Type | Constraints |
|-------|------|-------------|
| Banking Date | MudDatePicker | Required; Min: Today-14 days, Max: Today |
| Amount Banked | MudNumericField | Required; must be > 0 |
| Transaction Reference | MudTextField (multiline) | Required; bank/mobile payment reference |
| Attachment | File Upload | Optional; photos (max 10 images) |

**Default Values:**
- `BankingDate = DateTime.Now`
- `Item = "{DateTime.Now:dd MMM} Daily Banking"` (auto-generated description)
- `ButtonText = "Save"` (changes to "Update" in edit mode)
- `IsCancelVisible = false`

**Validation Rules:**
1. Transaction Reference: Required, not empty
2. Amount Banked: Must be > 0
3. Error message: "Please provide the reference/amount details..."

**Data Manipulation on Save:**
1. If Attachment exists:
   - Convert file to bytes
   - Base64 encode: `Banking.Base64FileData = Convert.ToBase64String(bytes)`
   - Set MIME type: `Banking.ContentType = GetMimeType(extension)`
2. Create `DateStamp = BankingDate.ToString("yyyyMMdd")`
3. Auto-generate Item: `Banking.Item = "{BankingDate:dd MMM} Daily Banking"`
4. If new banking: Set `QId = CurrentUser.QuarryId`
5. Save to database
6. Show toast: "New banking record has been captured!" or "Banking record has been updated!"
7. Reset form (with new auto-generated Item) and refresh list

**Edit Mode Activation:**
- On tap/click of banking item in list:
  - Load banking record into form
  - Set `ButtonText = "Update"`
  - Set `IsCancelVisible = true`

**Special UI Behaviors:**
- Combined form and list view on same page
- Attachment section in collapsible MudExpansionPanel
- Reference code truncated to 10 chars in list display (`RefCode` property)
- Pagination: Load 12 items initially, infinite scroll for more
- Banking list sorted by BankingDate descending
- Each list item shows: Date/Time, Item description, Amount (bold, primary), Reference code
- Delete confirmation dialog before removal

**Deliverables:**
- Complete banking record management

---

### 2.5 Fuel Usage Tracking

**Tasks:**
- [ ] Create `Features/FuelUsage/Pages/FuelUsage.razor`
- [ ] Create `Features/FuelUsage/Components/FuelForm.razor`
- [ ] Create `Features/FuelUsage/Services/FuelUsageService.cs`
- [ ] Implement real-time balance calculation:
  - Total Stock = Old Stock + New Stock
  - Used = Machines + Wheel Loaders
  - Balance = Total - Used
- [ ] Create usage history list with edit

**UI Logic from Legacy MAUI App:**

**Form Fields:**
| Field | Type | Constraints |
|-------|------|-------------|
| Old Stock | MudNumericField | Required; liters brought forward |
| New Stock | MudNumericField | Optional; new fuel added |
| Machines Loaded | MudNumericField | Required; liters used by machines |
| Wheel Loaders Loaded | MudNumericField | Required; liters used by wheel loaders |
| Usage Date | MudDatePicker | Required; Min: Today-14 days, Max: Today |

**Real-time Calculations (triggered on any field change):**
```
TotalStock = OldStock + NewStock
Used = MachinesLoaded + WheelLoadersLoaded
Balance = TotalStock - Used
```

**Default Values:**
- `UsageDate = DateTime.Now`
- All numeric fields default to 0

**Data Manipulation on Save:**
1. Create `DateStamp = UsageDate.ToString("yyyyMMdd")`
2. Set `QId = CurrentUser.QuarryId`
3. Set `ApplicationUserId = CurrentUser.UserId`
4. Save to database
5. Show toast and refresh list

**Special UI Behaviors:**
- Combined form and list view on same page
- Real-time calculated fields displayed (Total Stock, Balance)
- Balance displayed prominently with "ltrs" suffix
- Usage history list with edit on tap
- Pagination similar to other lists

**Deliverables:**
- Fuel usage tracking with calculations
- History list

---

### 2.6 Clerk Reports

**Tasks:**
- [ ] Create `Features/Reports/Pages/ClerkReport.razor`
- [ ] Create `Features/Reports/Services/ReportService.cs`:
  - `GenerateClerkReportAsync(fromDate, toDate, quarryId, clerkId)`
  - `GetSalesForReportAsync()`
  - `GetExpensesForReportAsync()`
  - `GetBankingForReportAsync()`
  - `GetFuelUsageForReportAsync()`
  - `CalculateReportSummary()`
- [ ] Create `Features/Reports/Components/`:
  - `ReportSummary.razor` - Full summary display
  - `SalesTable.razor` - Sales list with unpaid highlighting
  - `ExpensesTable.razor` - Expenses list
  - `FuelUsageTable.razor` - Fuel history
  - `BankingTable.razor` - Banking records
- [ ] Implement share/export functionality

**UI Logic from Legacy MAUI App:**

**Form Fields:**
| Field | Type | Constraints |
|-------|------|-------------|
| From Date | MudDatePicker | Required; start date for report |
| To Date | MudDatePicker | Required; end date for report |
| Get Report Button | MudButton | Triggers report generation |

**Default Values:**
- `FromDate = DateTime.Now`
- `ToDate = DateTime.Now`
- `ShareVisible = false` (hidden until report generated)
- `LandRateVisible = true` (based on quarry settings)

**Report Calculations (executed in GetReport command):**

**1. Opening Balance (same-day reports only):**
```
if (FromDate.Date == ToDate.Date):
    note = GetOpeningBalanceAsync(FromDate)  // Previous day's closing
    OpeningBalance = note?.ClosingBalance ?? 0
else:
    OpeningBalance = 0
```

**2. Sales Totals:**
```
TotalQuantity = Sum(Sales.Quantity)
TotalSales = Sum(Sales.Amount)
Unpaid = Sum(Sales.Amount WHERE Paid == false)
UnpaidOrders = (Unpaid > 0)
```

**3. Expense Breakdown (from 4 sources - CRITICAL):**
```
// Expenses come from 4 different sources for reporting:
// 1. User manual expenses (LineType = "User Expense")
// 2. Commission from sales (LineType = "Commission Expense")
// 3. Loaders fee from sales (LineType = "Loaders Fee Expense")
// 4. Land rate from sales (LineType = "Land Rate Fee Expense")

TotalExpenses = Sum(AllExpenses.Amount)
LoadersFee = Sum(Expenses WHERE LineType == "Loaders Fee Expense")
Commission = Sum(Expenses WHERE LineType == "Commission Expense")
LandRateFee = Sum(Expenses WHERE LineType == "Land Rate Fee Expense")
```

**4. Earnings & Net Income:**
```
Earnings = TotalSales - TotalExpenses
NetEarnings = (Earnings + OpeningBalance) - Unpaid
```

**5. Banking & Cash in Hand:**
```
Banked = Sum(Bankings.AmountBanked)
CashInHand = NetEarnings - Banked
```

**6. Report Title:**
```
ReportTitle = FromDate == ToDate ?
              "{FromDate:dd/MM/yyyy}" :
              "{FromDate:dd/MM/yyyy} - {ToDate:dd/MM/yyyy}"
```

**7. LandRate Visibility:**
```
Quarry = GetQuarryAsync(QuarryId)
LandRateVisible = (Quarry.LandRateFee > 0)
```

**8. Closing Balance Update (same-day reports):**
```
if (FromDate.Date == ToDate.Date):
    note = GetOrCreateTodayNote(ToDate)
    note.ClosingBalance = CashInHand
    SaveDailyNoteAsync(note)
```

**Report Summary Grid Display:**
```
Opening Balance (B/F)     : {OpeningBalance:N0}
Quantity                  : {TotalQuantity:N0} pcs
Sales                     : {TotalSales:N0}
Commissions               : {Commission:N0}
Loaders Fee               : {LoadersFee:N0}
Land Rate                 : {LandRateFee:N0}  (conditionally visible)
Total Expenses            : {TotalExpenses:N0}
Earnings                  : {Earnings:N0}
Unpaid Orders             : {Unpaid:N0}  (RED if > 0)
Net Income                : {NetEarnings:N0}
Banked                    : {Banked:N0}
Closing Balance (C/H)     : {CashInHand:N0}  (BOLD, UNDERLINED)
```

**Share Functionality - Two Formats:**

**1. ShareReport() - Detailed:**
- Line-by-line sales with individual amounts
- Line-by-line expenses
- Detailed fuel usage records
- Detailed banking with references (truncated to 10 chars)

**2. ShareSummaryReport() - Grouped:**
- Sales grouped by product (quantity + amount)
- Aggregated expenses
- Summary financial figures
- Complete with opening/closing balance

**Special UI Behaviors:**
- Collapsible sections (MudExpansionPanels) for Sales, Expenses, Fuel, Banking
- Sales amounts: RED if unpaid, Primary color if paid
- Currency format: `{0:N1}` (one decimal)
- Quantity format: `{0:N0}pcs`
- Date format: `{0:dd/MM/yy}`
- Empty state messages for sections with no data
- Activity indicator during report generation
- Share buttons visible only after report generated
- Two share options: Detailed Report vs Summary Report

**Deliverables:**
- Complete clerk-level report with all sections
- Accurate summary calculations

---

## Phase 3: Manager/Admin Features

### 3.1 Analytics Dashboard

**Tasks:**
- [ ] Create `Features/Dashboard/Pages/ManagerDashboard.razor`
- [ ] Create `Features/Dashboard/Services/AnalyticsService.cs`:
  - `GetDashboardStatsAsync(quarryId, from, to)`
  - `GetSalesTrendsAsync()`
  - `GetProfitAnalysisAsync()`
- [ ] Create `wwwroot/js/charts.js` with Chart.js integration (see claude.md):
  - `createSalesChart()` - Bar/Line combo for Revenue vs Expenses
  - `createProfitGauge()` - Semi-circular gauge for profit margin
  - `createProductPieChart()` - Pie chart for product breakdown
- [ ] Create `Shared/Components/SalesChart.razor` - Blazor wrapper for Chart.js
- [ ] Create `Shared/Components/ProfitGauge.razor` - Profit margin visualization
- [ ] Create `Shared/Components/ProductBreakdown.razor` - Product sales pie chart
- [ ] Implement:
  - Quarry selector
  - Date range filter
  - Metric cards with icons (Revenue, Orders, Quantity, Fuel) using MudCard
  - Sales performance chart (Chart.js bar/line combo)
  - Profit margin gauge (Chart.js doughnut)
  - Product breakdown pie chart
  - Detailed sales summary table with MudTable
- [ ] Add SignalR for real-time updates (optional)

**Deliverables:**
- Rich analytics dashboard with interactive Chart.js visualizations
- Multi-quarry support
- Mobile-responsive metric cards

---

### 3.2 Daily Sales View

**Tasks:**
- [ ] Create `Features/Reports/Pages/DailySales.razor`
- [ ] Implement:
  - Date range selection
  - Quarry filter
  - Daily breakdown table with drill-down
  - Totals row
  - Export to Excel button

**Deliverables:**
- Daily sales breakdown with export

---

### 3.3 Report Generator

**Tasks:**
- [ ] Create `Features/Reports/Pages/ReportGenerator.razor`
- [ ] Create `Features/Reports/Services/ExcelExportService.cs`:
  - `GenerateSalesReportAsync()` - Multi-worksheet Excel
  - `GenerateCashFlowReportAsync()`
- [ ] Implement:
  - Date range picker
  - Quarry selection
  - Report type selection
  - Download button
  - Email send button
- [ ] Use ClosedXML for Excel generation

**Deliverables:**
- Excel report generation matching legacy format
- Email delivery capability

---

### 3.4 Master Data Management

**Tasks:**
- [ ] Create `Features/MasterData/Pages/`:
  - `Quarries.razor` - CRUD for quarries (Manager only - own quarries)
  - `Products.razor` - Product management (shared, Manager can view)
  - `Layers.razor` - Layer management per quarry (Manager only - own quarries)
  - `Brokers.razor` - Broker management per quarry (Manager only - own quarries)
  - `Prices.razor` - Product prices per quarry (Manager only - own quarries)
- [ ] Create corresponding services and components
- [ ] Add validation for all forms
- [ ] Implement proper authorization (Manager for own quarries)

**Authorization Rules:**
| Entity | Administrator | Manager | Clerk |
|--------|--------------|---------|-------|
| Quarries | View all | CRUD (own) | View (assigned) |
| Products | View | View | View (assigned quarry) |
| Layers | View | CRUD (own quarries) | View (assigned quarry) |
| Brokers | View | CRUD (own quarries) | View (assigned quarry) |
| Prices | View | CRUD (own quarries) | View (assigned quarry) |

**Quarry Management (Manager Only):**
- Create new quarry → `ManagerId = CurrentUser.Id`
- Edit quarry settings (fees, email recipients, report schedule)
- View quarry statistics
- Cannot delete quarry with active sales data

**Data Filtering:**
```csharp
// All master data queries filtered by quarry ownership
GetLayers(quarryId):
    1. Verify currentUser owns quarryId OR is assigned to it
    2. Return layers WHERE QuarryId == quarryId

GetBrokers(quarryId):
    1. Verify currentUser owns quarryId OR is assigned to it
    2. Return brokers WHERE quarryId == quarryId
```

**Deliverables:**
- Complete master data management UI
- Quarry-scoped data access

---

### 3.5 User Management

**Tasks:**
- [ ] Create `Features/Admin/Pages/Managers.razor` (Administrator view)
- [ ] Create `Features/Admin/Pages/Users.razor` (Manager view)
- [ ] Create `Features/Admin/Services/UserService.cs`:
  - `GetManagersAsync()` - For Administrator
  - `GetUsersByQuarryAsync(quarryId)` - For Manager
  - `CreateManagerAsync()` - Administrator only
  - `CreateClerkAsync()` - Manager only
  - `UpdateUserAsync()`
  - `AssignUserToQuarryAsync()`
  - `RemoveUserFromQuarryAsync()`
  - `DeactivateUserAsync()`

**Role-Based User Management:**

**Administrator View (`/admin/managers`):**
- List all managers in the system
- Create new manager accounts
- View manager's quarries (read-only)
- Deactivate manager accounts

**Manager View (`/users`):**
- List clerks assigned to own quarries
- Create new clerk accounts
- Assign/unassign clerks to own quarries
- Invite other managers to own quarries (collaboration)
- Deactivate clerk accounts

**User Creation Flow:**

**Administrator Creating Manager:**
```
1. Enter: Full Name, Email, Phone, Position
2. System generates temporary password
3. Send welcome email with credentials
4. Manager logs in and sets own password
```

**Manager Creating Clerk:**
```
1. Select target quarry (from own quarries)
2. Enter: Full Name, Email, Phone, Position
3. System generates temporary password
4. Auto-assign clerk to selected quarry
5. Send welcome email with credentials
```

**User Assignment Logic:**
```csharp
// Manager can only assign users to quarries they own
AssignUserToQuarry(userId, quarryId):
    1. Verify currentUser is Manager
    2. Verify quarry.ManagerId == currentUser.Id
    3. Create UserQuarry record
    4. Set AssignedBy = currentUser.Id
    5. Set AssignedDate = DateTime.UtcNow
```

**Special UI Behaviors:**
- Administrators see only "Managers" menu item
- Managers see "Users" menu item (for their clerks)
- User list filtered by quarry for managers
- Quarry selector dropdown for managers with multiple quarries
- Confirmation dialog before deactivation
- Cannot deactivate self

**Deliverables:**
- Administrator manager management interface
- Manager clerk management interface
- Quarry-scoped user assignment

---

## Phase 4: API Layer

### 4.1 Minimal API Endpoints

**Tasks:**
- [ ] Create `Api/Endpoints/` folder with endpoint classes
- [ ] Implement endpoints for:
  - Authentication (`AuthEndpoints.cs`)
  - Sales (`SalesEndpoints.cs`)
  - Expenses (`ExpenseEndpoints.cs`)
  - Banking (`BankingEndpoints.cs`)
  - Fuel Usage (`FuelUsageEndpoints.cs`)
  - Reports (`ReportEndpoints.cs`)
  - Master Data (`MasterDataEndpoints.cs`)
  - Dashboard (`DashboardEndpoints.cs`)
- [ ] Add proper authorization to all endpoints
- [ ] Add validation using FluentValidation or DataAnnotations
- [ ] Implement proper error handling

**Note:** API endpoints are primarily for future mobile app or external integrations. The Blazor app will use services directly for most operations.

**Deliverables:**
- Complete REST API layer
- Swagger documentation

---

## Phase 5: Polish & Optimization

### 5.1 UI/UX Refinements (Mobile-First Approach)

**Tasks:**
- [ ] Implement mobile-first navigation patterns:
  - Clerk: Bottom navigation bar with FAB for new sale (see claude.md Component Patterns)
  - Manager: Collapsible sidebar navigation
- [ ] Create `wwwroot/css/app.css` with mobile-first responsive styles:
  - Base mobile styles (< 600px)
  - Tablet breakpoint (≥ 600px)
  - Desktop breakpoint (≥ 960px)
  - Large desktop (≥ 1280px)
- [ ] Implement MudBlazor responsive utilities:
  - `xs`, `sm`, `md`, `lg`, `xl` breakpoints on MudGrid/MudItem
  - `d-none`, `d-sm-block` visibility classes
- [ ] Add loading indicators (MudProgressCircular) for all async operations
- [ ] Implement toast notifications (MudSnackbar) for success/error
- [ ] Create touch-friendly targets (min 44x44px) for all interactive elements
- [ ] Style unpaid order rows with red highlight (see `.unpaid-row` in claude.md)
- [ ] Add dark mode support using MudThemeProvider toggle

**Deliverables:**
- Elegant, modern mobile-first UI
- Consistent Material Design 3 styling throughout
- Smooth touch interactions on mobile devices

---

### 5.2 PWA Enhancements

**Tasks:**
- [ ] Create `Features/Shared/Services/PushNotificationService.cs`:
  - `SubscribeAsync()` - Subscribe user to push notifications
  - `UnsubscribeAsync()` - Remove subscription
  - `SendNotificationAsync()` - Send push to specific user
  - Store subscriptions in database (PushSubscription entity)
- [ ] Create `Shared/Components/InstallPrompt.razor`:
  - Detect installability via `beforeinstallprompt` event
  - Show install banner for eligible users
  - Track installation state
- [ ] Create `Shared/Components/PwaUpdateNotification.razor`:
  - Detect service worker updates
  - Prompt user to refresh for new version
- [ ] Configure VAPID keys in `appsettings.json`:
  - Generate VAPID keys using web-push library
  - Add public key to client-side JavaScript
  - Add private key to server configuration
- [ ] Implement push notification triggers:
  - New sale recorded (notify managers)
  - Daily report ready
  - Banking confirmation
- [ ] Add PWA-specific CSS in `wwwroot/css/pwa.css`:
  - Safe area insets for notched devices
  - Standalone display optimizations
  - Touch-friendly targets (min 44x44px)
- [ ] Create JavaScript interop for PWA features (`wwwroot/js/pwa-interop.js`):
  - Install prompt handling
  - Service worker registration
  - Push subscription management

**Deliverables:**
- Push notifications for important events
- Smooth install experience
- Update notifications for new versions

---

### 5.3 Performance Optimization

**Tasks:**
- [ ] Add caching for master data (products, layers, brokers)
- [ ] Implement server-side pagination for all lists
- [ ] Optimize EF Core queries:
  - Add proper indexes
  - Use projection for list views
  - Avoid N+1 queries
- [ ] Add response compression

**Deliverables:**
- Fast, responsive application

---

### 5.4 Error Handling & Logging

**Tasks:**
- [ ] Configure Serilog for structured logging
- [ ] Implement global exception handler
- [ ] Add user-friendly error pages
- [ ] Add correlation IDs for request tracking
- [ ] Implement health checks

**Deliverables:**
- Robust error handling
- Comprehensive logging

---

## Phase 6: Testing & Deployment

### 6.1 Testing

**Tasks:**
- [ ] Create `tests/QDeskPro.Tests/` project
- [ ] Write unit tests for:
  - Calculation services
  - Validation rules
  - Entity behavior
- [ ] Write integration tests for:
  - API endpoints
  - Database operations
- [ ] Manual testing of all user flows

**Deliverables:**
- Test coverage for critical paths

---

### 6.2 Deployment Setup

**Tasks:**
- [ ] Create production `appsettings.Production.json`
- [ ] Set up database migration strategy
- [ ] Configure HTTPS
- [ ] Set up CI/CD pipeline (optional)
- [ ] Create deployment documentation

**Deliverables:**
- Deployment-ready application

---

## Implementation Order Summary

```
Phase 1 (Foundation)
├── Project setup
├── Database layer
├── Authentication
└── PWA foundation (manifest, service worker, icons)

Phase 2 (Clerk Operations)
├── Clerk dashboard
├── Sales entry
├── Expenses
├── Banking
├── Fuel usage
└── Clerk reports

Phase 3 (Manager Features)
├── Analytics dashboard
├── Daily sales view
├── Report generator
├── Master data management
└── User management

Phase 4 (API Layer)
└── Minimal API endpoints

Phase 5 (Polish & Optimization)
├── UI/UX refinements
├── PWA enhancements (push notifications, install prompt)
├── Performance optimization
├── Error handling & logging
└── Testing

Phase 6 (Deployment)
└── Production deployment
```

---

## File Creation Order

### First Files to Create

1. `QDeskPro.sln`
2. `src/QDeskPro/QDeskPro.csproj`
3. `src/QDeskPro/Program.cs`
4. `src/QDeskPro/appsettings.json`
5. `src/QDeskPro/Domain/Entities/BaseEntity.cs`
6. `src/QDeskPro/Domain/Entities/ApplicationUser.cs`
7. `src/QDeskPro/Domain/Entities/Quarry.cs`
8. `src/QDeskPro/Domain/Entities/Sale.cs`
9. `src/QDeskPro/Data/AppDbContext.cs`

### Directory Structure to Create

```
src/QDeskPro/
├── Api/
│   └── Endpoints/
├── Data/
│   ├── Migrations/
│   └── Seed/
├── Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Services/
├── Features/
│   ├── Auth/
│   │   ├── Components/
│   │   ├── Pages/
│   │   └── Services/
│   ├── Dashboard/
│   ├── Sales/
│   ├── Expenses/
│   ├── Banking/
│   ├── FuelUsage/
│   ├── Reports/
│   ├── MasterData/
│   └── Admin/
├── Shared/
│   ├── Components/
│   ├── Layouts/
│   └── Extensions/
└── wwwroot/
    ├── css/
    │   ├── app.css
    │   └── pwa.css
    ├── js/
    │   ├── charts.js
    │   └── pwa-interop.js
    ├── icons/
    │   ├── icon-192x192.png
    │   ├── icon-512x512.png
    │   ├── icon-maskable-192x192.png
    │   └── icon-maskable-512x512.png
    ├── manifest.json
    ├── service-worker.js
    ├── service-worker.published.js
    └── offline.html
```

---

## Migration from Legacy Data

If migrating existing QDesk data:

1. Create migration script to copy data from legacy SQL Server database
2. Map legacy entity IDs to new schema
3. Preserve all historical data
4. Validate data integrity after migration
5. Test with migrated data before go-live

---

## Success Criteria

- [ ] All clerk operations (sales, expenses, banking, fuel) work correctly
- [ ] All calculations match legacy system exactly
- [ ] Reports generate correctly with accurate totals
- [ ] Mobile-responsive UI works on tablets
- [ ] Manager dashboard shows accurate analytics
- [ ] User authentication and authorization work correctly
- [ ] Performance meets targets (< 500ms page loads)
- [ ] Zero data loss from legacy system (if migrating)
- [ ] PWA installable on mobile devices (passes Lighthouse PWA audit)
- [ ] Service worker caches static assets for faster loads
- [ ] Push notifications delivered successfully to subscribed users
- [ ] Offline fallback page displays when network unavailable
