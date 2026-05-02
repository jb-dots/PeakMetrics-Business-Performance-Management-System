# Design Document: Role-Based Dashboards

## Overview

This document describes the technical design for replacing the single generic `Dashboard` view in PeakMetrics with five distinct, role-tailored dashboards. Each dashboard is purpose-built for one of the five session roles: `Admin`, `Administrator`, `Manager`, `User`, and `Executive`.

The implementation stays entirely within the existing ASP.NET Core 8 MVC + EF Core + SQL Server stack. No new controllers, no new routes, and no schema changes are required. The single `HomeController.Dashboard` action becomes a dispatcher that reads the session role and delegates to a role-specific view model builder. Each role gets its own strongly-typed ViewModel and its own Razor partial view rendered inside the existing `Dashboard.cshtml` shell.

Two new seeded accounts (`executive@peakmetrics.com`) are added via a new EF Core migration. The sidebar's KPI Management gate is widened to include the `Administrator` role alongside the existing `Admin` and `Manager` check.

---

## Architecture

### Dashboard Routing Pattern

The `Dashboard` action in `HomeController` reads `HttpContext.Session.GetString("UserRole")` and switches to the appropriate private async builder method. Each builder returns an `IActionResult` directly, keeping the action body thin.

```
GET /Home/Dashboard
        │
        ▼
  HomeController.Dashboard()
        │  reads session role
        ├─ "Admin"         → BuildSuperAdminDashboardAsync()  → View("Dashboard", SuperAdminDashboardViewModel)
        ├─ "Administrator" → BuildAdminDashboardAsync()       → View("Dashboard", AdministratorDashboardViewModel)
        ├─ "Manager"       → BuildManagerDashboardAsync()     → View("Dashboard", ManagerDashboardViewModel)
        ├─ "User"          → BuildStaffDashboardAsync()       → View("Dashboard", StaffDashboardViewModel)
        ├─ "Executive"     → BuildExecutiveDashboardAsync()   → View("Dashboard", ExecutiveDashboardViewModel)
        └─ (unknown)       → RedirectToAction("Login")
```

`Dashboard.cshtml` becomes a thin dispatcher view that checks the model type and renders the matching partial:

```razor
@if (Model is SuperAdminDashboardViewModel)      { <partial name="_SuperAdminDashboard"      model="(SuperAdminDashboardViewModel)Model" /> }
else if (Model is AdministratorDashboardViewModel) { <partial name="_AdministratorDashboard" model="(AdministratorDashboardViewModel)Model" /> }
else if (Model is ManagerDashboardViewModel)       { <partial name="_ManagerDashboard"       model="(ManagerDashboardViewModel)Model" /> }
else if (Model is StaffDashboardViewModel)         { <partial name="_StaffDashboard"         model="(StaffDashboardViewModel)Model" /> }
else if (Model is ExecutiveDashboardViewModel)     { <partial name="_ExecutiveDashboard"     model="(ExecutiveDashboardViewModel)Model" /> }
```

The `@model` directive in `Dashboard.cshtml` is changed to `@model object` to accept any of the five view model types.

### Sidebar Role-Gating Update

The `_Layout.cshtml` currently gates the KPI Management link with:

```csharp
var isManagerOrAdmin = sessionUserRole is "Admin" or "Manager";
```

This is updated to:

```csharp
var canManageKpis = sessionUserRole is "Admin" or "Administrator" or "Manager";
```

All three references to `isManagerOrAdmin` in the layout (desktop nav, mobile nav, and the variable declaration) are updated to use `canManageKpis`.

### Chart.js Data Serialisation

Chart data is passed from the ViewModel to JavaScript via `data-*` attributes on the canvas element, serialised as JSON using `System.Text.Json`. This avoids inline `<script>` blocks in partials and keeps the JavaScript in `site.js` generic.

Pattern used in partials:

```razor
<canvas id="managerTrendChart"
        data-labels="@Json.Serialize(Model.TrendLabels)"
        data-datasets="@Json.Serialize(Model.TrendDatasets)"></canvas>
```

Pattern used in `site.js`:

```javascript
function initChartFromCanvas(canvasId, type, options = {}) {
    const el = document.getElementById(canvasId);
    if (!el) return;
    const labels   = JSON.parse(el.dataset.labels   || '[]');
    const datasets = JSON.parse(el.dataset.datasets || '[]');
    new Chart(el, { type, data: { labels, datasets }, options });
}
```

`Json.Serialize` is `System.Text.Json.JsonSerializer.Serialize`, exposed in Razor via a static helper or `@using System.Text.Json` + `JsonSerializer.Serialize(...)`.

---

## Components and Interfaces

### HomeController Changes

| Method | Change |
|---|---|
| `Dashboard()` | Becomes a role dispatcher; delegates to five private builder methods |
| `BuildSuperAdminDashboardAsync()` | New private method |
| `BuildAdminDashboardAsync()` | New private method |
| `BuildManagerDashboardAsync()` | New private method |
| `BuildStaffDashboardAsync()` | New private method |
| `BuildExecutiveDashboardAsync()` | New private method |
| `IsManagerOrAdmin()` | Renamed/replaced by `CanManageKpis()` that includes `Administrator` |

### New ViewModels

All new ViewModels live in the `ViewModels/` folder.

**`SuperAdminDashboardViewModel`**
```csharp
public sealed class SuperAdminDashboardViewModel
{
    public int TotalUsers        { get; init; }
    public int TotalDepartments  { get; init; }
    public int TotalKpis         { get; init; }
    public int TotalAuditEntries { get; init; }

    // System Health Snapshot
    public int TotalNotifications  { get; init; }
    public int TotalKpiLogEntries  { get; init; }

    // Recent Audit Log (top 10)
    public IReadOnlyList<AuditLogRowViewModel> RecentAuditLogs { get; init; } = Array.Empty<AuditLogRowViewModel>();

    // Role distribution chart: key = role string, value = count
    public IReadOnlyDictionary<string, int> RoleDistribution { get; init; } = new Dictionary<string, int>();

    // Department overview table
    public IReadOnlyList<DepartmentOverviewViewModel> Departments { get; init; } = Array.Empty<DepartmentOverviewViewModel>();
}
```

**`AdministratorDashboardViewModel`**
```csharp
public sealed class AdministratorDashboardViewModel
{
    public int TotalUsers        { get; init; }
    public int TotalDepartments  { get; init; }
    public int PendingKpis       { get; init; }
    public int NewUsersThisMonth { get; init; }
    public int UnreadNotifications { get; init; }

    // User list overview (top 10 most recently created)
    public IReadOnlyList<UserRowViewModel> RecentUsers { get; init; } = Array.Empty<UserRowViewModel>();

    // Department summary table
    public IReadOnlyList<DepartmentOverviewViewModel> Departments { get; init; } = Array.Empty<DepartmentOverviewViewModel>();
}
```

**`ManagerDashboardViewModel`**
```csharp
public sealed class ManagerDashboardViewModel
{
    public int TotalKpis         { get; init; }
    public int OnTrack           { get; init; }
    public int AtRisk            { get; init; }
    public int Behind            { get; init; }
    public int UnreadNotifications { get; init; }

    // Bar chart: department name → status counts
    public IReadOnlyList<DeptKpiStatusViewModel> DeptKpiStatuses { get; init; } = Array.Empty<DeptKpiStatusViewModel>();

    // Trend line chart: one dataset per department, 6 months
    public IReadOnlyList<string>                  TrendLabels   { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TrendDatasetViewModel>   TrendDatasets { get; init; } = Array.Empty<TrendDatasetViewModel>();

    // Strategic goals (active only)
    public IReadOnlyList<StrategicGoalRowViewModel> ActiveGoals { get; init; } = Array.Empty<StrategicGoalRowViewModel>();

    // Recent KPI log entries (top 5)
    public IReadOnlyList<RecentKpiLogViewModel> RecentLogs { get; init; } = Array.Empty<RecentKpiLogViewModel>();
}
```

**`StaffDashboardViewModel`**
```csharp
public sealed class StaffDashboardViewModel
{
    public int MyKpis            { get; init; }
    public int OnTrack           { get; init; }
    public int AtRisk            { get; init; }
    public int Behind            { get; init; }
    public int UnreadNotifications { get; init; }
    public bool HasDepartment    { get; init; }

    // My KPI list (all active KPIs in user's department)
    public IReadOnlyList<KpiRowViewModel> MyKpiList { get; init; } = Array.Empty<KpiRowViewModel>();

    // Due for logging (Pending_KPIs in user's department)
    public IReadOnlyList<string> PendingKpiNames { get; init; } = Array.Empty<string>();

    // My recent logs (top 5 by current user)
    public IReadOnlyList<RecentKpiLogViewModel> MyRecentLogs { get; init; } = Array.Empty<RecentKpiLogViewModel>();

    // My scorecard: perspective → status counts
    public IReadOnlyList<ScorecardPerspectiveViewModel> Scorecard { get; init; } = Array.Empty<ScorecardPerspectiveViewModel>();
}
```

**`ExecutiveDashboardViewModel`**
```csharp
public sealed class ExecutiveDashboardViewModel
{
    public int    OverallPerformancePct  { get; init; }
    public int    TotalKpis             { get; init; }
    public int    ActiveGoals           { get; init; }
    public int    DepartmentsTracked    { get; init; }

    // BSC perspective cards (4 items)
    public IReadOnlyList<BscPerspectiveViewModel> BscPerspectives { get; init; } = Array.Empty<BscPerspectiveViewModel>();

    // Doughnut chart: On Track / At Risk / Behind counts
    public int ChartOnTrack { get; init; }
    public int ChartAtRisk  { get; init; }
    public int ChartBehind  { get; init; }

    // Top performing departments (all, ranked descending by score)
    public IReadOnlyList<DeptPerformanceViewModel> TopDepartments { get; init; } = Array.Empty<DeptPerformanceViewModel>();

    // Underperforming KPIs (latest status == "Behind")
    public IReadOnlyList<UnderperformingKpiViewModel> UnderperformingKpis { get; init; } = Array.Empty<UnderperformingKpiViewModel>();
}
```

### Supporting Row/Item ViewModels

```csharp
public sealed record AuditLogRowViewModel(string UserName, string Action, string EntityType, string RelativeTime);
public sealed record DepartmentOverviewViewModel(string Name, int UserCount, int ActiveKpiCount);
public sealed record UserRowViewModel(string FullName, string Role, string DepartmentName, string CreatedAt);
public sealed record DeptKpiStatusViewModel(string Department, int OnTrack, int AtRisk, int Behind);
public sealed record TrendDatasetViewModel(string DepartmentName, IReadOnlyList<decimal?> Values);
public sealed record StrategicGoalRowViewModel(string Title, string Perspective, string Status, string? DueDate);
public sealed record RecentKpiLogViewModel(string KpiName, string LoggedBy, string ActualWithUnit, string Status, string RelativeTime);
public sealed record KpiRowViewModel(string Name, string Perspective, string Target, string Actual, string Status);
public sealed record ScorecardPerspectiveViewModel(string Perspective, int OnTrack, int AtRisk, int Behind);
public sealed record BscPerspectiveViewModel(string Perspective, int OnTrack, int AtRisk, int Behind);
public sealed record DeptPerformanceViewModel(string DepartmentName, int ScorePct);
public sealed record UnderperformingKpiViewModel(string KpiName, string DepartmentName, string Target, string Actual);
```

### New Partial Views

| File | Rendered for role |
|---|---|
| `Views/Home/_SuperAdminDashboard.cshtml` | `Admin` |
| `Views/Home/_AdministratorDashboard.cshtml` | `Administrator` |
| `Views/Home/_ManagerDashboard.cshtml` | `Manager` |
| `Views/Home/_StaffDashboard.cshtml` | `User` |
| `Views/Home/_ExecutiveDashboard.cshtml` | `Executive` |

Each partial is strongly typed (`@model <RoleViewModel>`) and self-contained. Charts are initialised by `site.js` reading `data-*` attributes on canvas elements.

---

## Data Models

No schema changes are required. All five dashboards are built from the existing EF Core models. The table below maps each dashboard to the models it queries.

| Dashboard | Models Queried |
|---|---|
| Super Admin | `AppUser`, `Department`, `Kpi`, `AuditLog`, `Notification`, `KpiLogEntry` |
| Administrator | `AppUser`, `Department`, `Kpi`, `KpiLogEntry`, `Notification` |
| Manager | `Kpi`, `KpiLogEntry`, `Department`, `StrategicGoal`, `Notification` |
| Staff | `Kpi`, `KpiLogEntry`, `Notification` (scoped to `DepartmentId`) |
| Executive | `Kpi`, `KpiLogEntry`, `Department`, `StrategicGoal` |

### Key Query Patterns

**Latest KPI status per KPI** (used by Manager, Staff, Executive):
```csharp
var latestByKpi = await _db.KpiLogEntries
    .GroupBy(e => e.KpiId)
    .Select(g => g.OrderByDescending(e => e.LoggedAt).First())
    .ToListAsync(ct);
```

**Pending KPIs** (used by Administrator summary card and Staff dashboard):
A KPI is pending when it has no `KpiLogEntry` whose `LoggedAt` falls within the current calendar month. The current period is determined server-side as `DateTime.UtcNow` month/year.
```csharp
var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
var loggedKpiIds = await _db.KpiLogEntries
    .Where(e => e.LoggedAt >= currentMonth)
    .Select(e => e.KpiId)
    .Distinct()
    .ToListAsync(ct);
var pendingKpis = activeKpis.Where(k => !loggedKpiIds.Contains(k.Id)).ToList();
```

**Manager trend chart — per-department monthly averages (last 6 months)**:
```csharp
var sixMonthsAgo = DateTime.UtcNow.AddMonths(-5);
var cutoff = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

var entries = await _db.KpiLogEntries
    .Where(e => e.LoggedAt >= cutoff)
    .Include(e => e.Kpi).ThenInclude(k => k.Department)
    .ToListAsync(ct);

// Group by (DepartmentName, Year, Month) → average ActualValue
// Build 6 month label list; fill null for months with no data
```

The 6 month labels are generated as `["Nov 2024", "Dec 2024", "Jan 2025", ...]` from the current month backwards. Each department becomes one `TrendDatasetViewModel` with a `decimal?` value per month slot (null = no data, rendered as a gap in Chart.js with `spanGaps: false`).

**Overall Performance Percentage** (Executive):
```
OverallPerformancePct = Round( OnTrackCount / KpisWithAtLeastOneEntry * 100 )
```
If `KpisWithAtLeastOneEntry == 0`, the value is 0.

**Department Performance Score** (Executive):
```
ScorePct = Round( OnTrackKpisInDept / TotalKpisWithEntriesInDept * 100 )
```
Departments with no KPI log entries are included with a score of 0.

### Seeding — New Migration

A new EF Core migration `SeedExecutiveAndAdministrator` adds two `AppUser` rows. The BCrypt hashes are pre-computed at work factor 11 (matching existing seeds) and embedded directly in the migration, following the same pattern as `SeedRealAccounts`.

| Id | FullName | Email | Password (plain) | Role | DepartmentId |
|---|---|---|---|---|---|
| 6 | Executive User | executive@peakmetrics.com | Executive@123 | Executive | null |

The migration uses `migrationBuilder.InsertData` and does **not** touch `OnModelCreating` seed data, keeping the two seeding mechanisms separate and avoiding snapshot conflicts.

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Dashboard routing covers all valid roles

*For any* role string in `{ "Admin", "Administrator", "Manager", "User", "Executive" }`, the `Dashboard` action should return a view result whose model is an instance of the view model type that corresponds to that role, and should never redirect to Login.

**Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6**

---

### Property 2: Unknown role redirects to Login

*For any* string that is not in `{ "Admin", "Administrator", "Manager", "User", "Executive" }`, the `Dashboard` action should return a redirect result pointing to the Login action.

**Validates: Requirements 1.7**

---

### Property 3: Super Admin summary counts match database state

*For any* database state, the `SuperAdminDashboardViewModel` properties `TotalUsers`, `TotalDepartments`, `TotalKpis`, `TotalAuditEntries`, `TotalNotifications`, and `TotalKpiLogEntries` should each equal the actual count of the corresponding rows in the database.

**Validates: Requirements 2.1, 2.5**

---

### Property 4: Super Admin role distribution is a correct partition

*For any* set of users with various roles, the `RoleDistribution` dictionary in `SuperAdminDashboardViewModel` should correctly group users by role, and the sum of all values should equal `TotalUsers`.

**Validates: Requirements 2.3**

---

### Property 5: Department overview counts are accurate

*For any* set of departments, users, and KPIs, each `DepartmentOverviewViewModel` row should report a `UserCount` equal to the number of users whose `DepartmentId` matches that department, and an `ActiveKpiCount` equal to the number of active KPIs whose `DepartmentId` matches that department.

**Validates: Requirements 2.4, 3.3**

---

### Property 6: KPI status counts reflect the latest log entry per KPI

*For any* set of active KPIs and their log entries, the `OnTrack`, `AtRisk`, and `Behind` counts in any dashboard view model should equal the count of KPIs whose most recent `KpiLogEntry.Status` equals the respective status string. KPIs with no log entries should not contribute to any status count.

**Validates: Requirements 4.1, 5.1**

---

### Property 7: Staff dashboard is scoped to the user's department

*For any* user with a non-null `DepartmentId` and any set of KPIs spread across multiple departments, every KPI appearing in `StaffDashboardViewModel.MyKpiList` should have `DepartmentId` equal to the logged-in user's `DepartmentId`, and no KPI from a different department should appear.

**Validates: Requirements 5.2, 8.1**

---

### Property 8: Pending KPI detection is correct

*For any* set of active KPIs and log entries, a KPI should appear in the pending list if and only if it has no `KpiLogEntry` with `LoggedAt` in the current calendar month. A KPI with at least one entry in the current month should never appear as pending.

**Validates: Requirements 3.1 (Pending KPIs card), 5.3**

---

### Property 9: Manager trend data contains exactly 6 month slots per department

*For any* set of KPI log entries spanning various months and departments, the `TrendDatasets` in `ManagerDashboardViewModel` should contain one dataset per department that has at least one log entry in the 6-month window, and each dataset should have exactly 6 values (one per month slot, `null` for months with no data).

**Validates: Requirements 4.3**

---

### Property 10: Strategic goals filter excludes Cancelled and Completed

*For any* set of `StrategicGoal` records with various statuses, the `ActiveGoals` list in `ManagerDashboardViewModel` should contain exactly those goals whose `Status` is neither `"Cancelled"` nor `"Completed"`.

**Validates: Requirements 4.4**

---

### Property 11: Overall Performance Percentage is correctly computed

*For any* set of active KPIs and log entries, `ExecutiveDashboardViewModel.OverallPerformancePct` should equal `Round(onTrackCount / kpisWithEntries * 100)`, where `kpisWithEntries` is the count of active KPIs that have at least one log entry. When `kpisWithEntries` is zero, the result should be zero.

**Validates: Requirements 6.1**

---

### Property 12: Top departments are ranked by performance score descending

*For any* set of departments and KPI log entries, the `TopDepartments` list in `ExecutiveDashboardViewModel` should be ordered such that no department appears before a department with a strictly higher `ScorePct`.

**Validates: Requirements 6.4**

---

### Property 13: Underperforming KPIs list contains only Behind KPIs

*For any* set of active KPIs and log entries, every entry in `ExecutiveDashboardViewModel.UnderperformingKpis` should correspond to a KPI whose most recent `KpiLogEntry.Status` is `"Behind"`, and every active KPI with a latest status of `"Behind"` should appear in the list.

**Validates: Requirements 6.5**

---

### Property 14: Role string round-trip for new roles

*For any* role string in `{ "Executive", "Administrator" }`, creating an `AppUser` with that role and reading it back from the database should return the same role string unchanged.

**Validates: Requirements 7.2**

---

### Property 15: Unread notification count is user-scoped

*For any* set of notifications belonging to multiple users, the unread notification count in a dashboard view model should equal the count of `Notification` records where `UserId` equals the logged-in user's ID and `IsRead` is `false`. Notifications belonging to other users should not be counted.

**Validates: Requirements 4.6, 5.6**

---

## Error Handling

### Unknown Role
If `HttpContext.Session.GetString("UserRole")` returns `null`, an empty string, or any value not in the five defined roles, `Dashboard()` redirects to `Login`. This is the same guard already applied to unauthenticated requests.

### Staff User with No Department
When `StaffDashboardViewModel.HasDepartment` is `false` (i.e., `AppUser.DepartmentId` is `null`), all collection properties are empty and the partial view renders empty-state placeholders for every section. No exception is thrown.

### No KPI Log Entries
All status counts default to zero. `OverallPerformancePct` returns 0. Trend chart datasets are empty arrays. Chart.js renders an empty canvas gracefully.

### No Strategic Goals
`ActiveGoals` is an empty list. The partial renders an empty-state placeholder.

### Database Query Failures
EF Core exceptions propagate to ASP.NET Core's default exception handler. No special try/catch is added in the controller — the existing unhandled exception middleware handles this consistently with the rest of the application.

---

## Testing Strategy

### Unit Tests

Unit tests cover the five builder methods in isolation using an in-memory EF Core database (or a test double). Each test seeds a specific database state and asserts the resulting view model properties.

Key unit test scenarios:
- Each role returns the correct view model type (covers routing dispatch)
- Unknown role returns `RedirectToActionResult` to Login
- `SuperAdminDashboardViewModel` counts match seeded data
- `StaffDashboardViewModel` with `DepartmentId = null` returns empty collections
- `OverallPerformancePct` is 0 when no KPIs have log entries
- `PendingKpiNames` excludes KPIs that have a log entry in the current month
- Empty-state messages are triggered when collections are empty

### Property-Based Tests

Property-based tests use **FsCheck** (the .NET property-based testing library) with a minimum of 100 iterations per property. Each test is tagged with a comment referencing the design property it validates.

**Feature: role-based-dashboards**

| Property Test | Design Property |
|---|---|
| Dashboard routing covers all valid roles | Property 1 |
| Unknown role redirects to Login | Property 2 |
| Super Admin summary counts match database state | Property 3 |
| Super Admin role distribution is a correct partition | Property 4 |
| Department overview counts are accurate | Property 5 |
| KPI status counts reflect the latest log entry per KPI | Property 6 |
| Staff dashboard is scoped to the user's department | Property 7 |
| Pending KPI detection is correct | Property 8 |
| Manager trend data contains exactly 6 month slots per department | Property 9 |
| Strategic goals filter excludes Cancelled and Completed | Property 10 |
| Overall Performance Percentage is correctly computed | Property 11 |
| Top departments are ranked by performance score descending | Property 12 |
| Underperforming KPIs list contains only Behind KPIs | Property 13 |
| Role string round-trip for new roles | Property 14 |
| Unread notification count is user-scoped | Property 15 |

Tag format used in test code:
```csharp
// Feature: role-based-dashboards, Property 7: Staff dashboard is scoped to the user's department
```

FsCheck generators are written for `AppUser`, `Kpi`, `KpiLogEntry`, `Department`, and `StrategicGoal` to produce random but structurally valid instances. All tests run against an in-memory EF Core database to avoid external dependencies.

### Integration Tests

Integration tests (single execution, not property-based) cover:
- The new migration applies cleanly and the two new seeded accounts can authenticate
- The sidebar KPI Management link is visible for `Admin`, `Administrator`, and `Manager` roles and hidden for `User` and `Executive`
- Each role's dashboard page returns HTTP 200 with the correct partial view rendered

### Visual / Manual Tests

- Verify Chart.js renders correctly for each chart type (doughnut, bar, multi-line) with real data
- Verify empty-state placeholders display correctly when sections have no data
- Verify responsive layout on mobile (Bootstrap 5 grid) for all five dashboards
