# QDeskPro - Quarry Management System

A modern, unified Blazor web application for quarry sales management, built on .NET 10.

## Overview

QDeskPro consolidates daily sales operations for quarry clerks and backend analytics/reporting for managers into a single, streamlined web application.

### Key Features

- **Sales Recording**: Track sales transactions with vehicle registration, product type, quantity, pricing, and payment details
- **Expense Tracking**: Manual expenses, commissions, loaders' fees, and land rate fees
- **Banking Records**: Track cash deposits and banking transactions
- **Fuel Usage Monitoring**: Track fuel consumption for machines and wheel loaders
- **Reports & Analytics**: Daily, weekly, and monthly sales reports with Excel export
- **PWA Support**: Installable progressive web app with offline capabilities

## Technology Stack

- **Framework**: .NET 10 with Blazor Web App (Interactive Server + WebAssembly)
- **UI Library**: MudBlazor v8+ (Material Design 3)
- **Database**: SQL Server with Entity Framework Core 10
- **Authentication**: ASP.NET Core Identity
- **Reporting**: ClosedXML for Excel export

## User Roles

| Role | Description |
|------|-------------|
| **Administrator** | Creates and manages Manager accounts |
| **Manager** | Owns and manages quarries, creates clerks, views analytics |
| **Clerk** | Records daily operations (sales, expenses, banking, fuel) |

## Project Structure

```
QDeskPro/
├── src/QDeskPro/          # Main Blazor Web App
│   ├── Api/               # Minimal API endpoints
│   ├── Data/              # EF Core context and migrations
│   ├── Domain/            # Entities, enums, services
│   ├── Features/          # Feature-based organization
│   ├── Shared/            # Shared components and layouts
│   └── wwwroot/           # Static assets, PWA files
└── tests/                 # Test projects
```

## Documentation

- [CLAUDE.md](CLAUDE.md) - Complete project specification and domain model
- [implementation_plan.md](implementation_plan.md) - Detailed implementation plan

## Getting Started

### Prerequisites

- .NET 10 SDK
- SQL Server 2019+ or Azure SQL
- Visual Studio 2022 or VS Code

### Setup

1. Clone the repository
2. Update connection string in `appsettings.json`
3. Run database migrations: `dotnet ef database update`
4. Run the application: `dotnet run`

## License

Proprietary - All rights reserved
