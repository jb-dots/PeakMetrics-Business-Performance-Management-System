# Implementation Plan: Role-Based Dashboards

## Overview

Replace the single generic `Dashboard` view with five role-tailored dashboards. The work proceeds in dependency order: seed data first, then ViewModels, then controller logic, then views, then JavaScript. Each step is independently testable before the next begins.

## Tasks

- [x] 1. Add EF Core migration to seed Executive account
  - Create a new migration class `SeedExecutiveAndAdministrator` in `Migrations/`
  - Use `migrationBuilder.InsertData` on the `Users` table following the same pattern as `SeedRealAccounts`
  - Insert Id=6 `executive@peakmetrics.com` / `Executive@123` / role `Executive` / DepartmentId null
  - Pre-compute BCrypt hashes at work factor 11 and embed them directly in the migration (do not call BCrypt at runtime)
  - Implement the `Down()` method using `migrationBuilder.DeleteData` for the row
  - Update `AppDbContextModelSnapshot.cs` to reflect the new row
  - _Requirements: 7.1, 7.2_

- [x] 2. Create supporting ViewModel record types
  - Add all 12 supporting record types to `ViewModels/` — one file per record or grouped logically
  - Records to create: `AuditLogRowViewModel`, `DepartmentOverviewViewModel`, `UserRowViewModel`, `DeptKpiStatusViewModel`, `TrendDatasetViewModel`, `StrategicGoalRowViewModel`, `RecentKpiLogViewModel`, `KpiRowViewModel`, `ScorecardPerspectiveViewModel`, `BscPerspectiveViewModel`, `DeptPerformanceViewModel`, `UnderperformingKpiViewModel`
  - Each record uses `sealed record` with positional or `init`-only properties exactly as specified in the design document
  - _Requirements: 2.2, 2.4, 3.2, 3.3, 4.2, 4.3, 4.4, 4.5, 5.2, 5.4, 5.5, 6.2, 6.4, 6.5_

- [x] 3. Create the five role-specific dashboard ViewModel classes
  - Add `SuperAdminDashboardViewModel.cs`, `AdministratorDashboardViewModel.cs`, `ManagerDashboardViewModel.cs`, `StaffDashboardViewModel.cs`, `ExecutiveDashboardViewModel.cs` to `ViewModels/`
  - Each class is `sealed` with `init`-only properties and collection defaults of `Array.Empty<T>()` or `new Dictionary<,>()`
  - Match property names and types exactly as specified in the design document
  - _Requirements: 2.1–2.5, 3.1–3.5, 4.1–4.6, 5.1–5.6, 6.1–6.5_

- [x] 4. Update `HomeController.Dashboard()` to be a role dispatcher
  - Change the existing `Dashboard` action body to read `HttpContext.Session.GetString(SessionUserRole)` and switch to the appropriate private builder method
  - Add a `default` / unknown-role branch that returns `RedirectToAction(nameof(Login))`
  - Keep `ViewData["Title"] = "Dashboard"` at the top of the action
  - Remove the old inline query logic from `Dashboard()` — it moves into the builder methods
  - _Requirements: 1.1–1.7_

- [x] 5. Add `BuildSuperAdminDashboardAsync()` to `HomeController`
  - Query counts: `Users.CountAsync()`, `Departments.CountAsync()`, `Kpis.CountAsync(k => k.IsActive)`, `AuditLogs.CountAsync()`, `Notifications.CountAsync()`, `KpiLogEntries.CountAsync()`
  - Query recent audit logs: top 10 by `OccurredAt` descending, include `User`, project to `AuditLogRowViewModel` using `ToRelativeTime`
  - Query role distribution: group `Users` by `Role`, project to `Dictionary<string, int>`
  - Query department overview: join `Departments` with user counts and active KPI counts, project to `IReadOnlyList<DepartmentOverviewViewModel>`
  - Return `View("Dashboard", new SuperAdminDashboardViewModel { ... })`
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [ ]* 5.1 Write property test — Super Admin summary counts match database state (Property 3)
    - **Property 3: Super Admin summary counts match database state**
    - Seed random counts of users, departments, KPIs, audit logs, notifications, and log entries using FsCheck generators
    - Assert each ViewModel count property equals the actual row count in the in-memory database
    - **Validates: Requirements 2.1, 2.5**

  - [ ]* 5.2 Write property test — Super Admin role distribution is a correct partition (Property 4)
    - **Property 4: Super Admin role distribution is a correct partition**
    - Generate random sets of users with varying roles; assert `RoleDistribution` values sum to `TotalUsers` and each key matches a role group
    - **Validates: Requirements 2.3**

- [x] 6. Add `BuildAdminDashboardAsync()` to `HomeController`
  - Compute `PendingKpis`: active KPIs with no `KpiLogEntry` in the current calendar month (use the pattern from the design's "Pending KPIs" query)
  - Compute `NewUsersThisMonth`: `Users.CountAsync(u => u.CreatedAt >= firstOfMonth)`
  - Compute `UnreadNotifications`: `Notifications.CountAsync(n => !n.IsRead)` (all users, per Req 3.5)
  - Query `RecentUsers`: top 10 by `CreatedAt` descending, project to `UserRowViewModel`
  - Query `Departments`: project to `IReadOnlyList<DepartmentOverviewViewModel>` (same helper as task 5)
  - Return `View("Dashboard", new AdministratorDashboardViewModel { ... })`
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [ ]* 6.1 Write property test — Pending KPI detection is correct (Property 8)
    - **Property 8: Pending KPI detection is correct**
    - Generate random sets of active KPIs and log entries with varying `LoggedAt` dates; assert a KPI appears as pending if and only if it has no entry in the current calendar month
    - **Validates: Requirements 3.1, 5.3**

  - [ ]* 6.2 Write property test — Department overview counts are accurate (Property 5)
    - **Property 5: Department overview counts are accurate**
    - Generate random departments, users, and KPIs; assert each `DepartmentOverviewViewModel` row reports correct `UserCount` and `ActiveKpiCount`
    - **Validates: Requirements 2.4, 3.3**

- [x] 7. Add `BuildManagerDashboardAsync()` to `HomeController`
  - Compute latest-entry-per-KPI using the `GroupBy` pattern from the design; derive `OnTrack`, `AtRisk`, `Behind` counts
  - Query `DeptKpiStatuses`: group latest entries by department, project to `IReadOnlyList<DeptKpiStatusViewModel>`
  - Build trend data: query entries from the last 6 calendar months, group by (department, year, month), compute average `ActualValue`, fill null for missing month slots, project to `TrendLabels` + `TrendDatasets`
  - Query `ActiveGoals`: `StrategicGoals` where `Status != "Cancelled" && Status != "Completed"`, project to `IReadOnlyList<StrategicGoalRowViewModel>`
  - Query `RecentLogs`: top 5 `KpiLogEntries` by `LoggedAt` descending, include `Kpi` and `LoggedBy`, project to `IReadOnlyList<RecentKpiLogViewModel>`
  - Compute `UnreadNotifications` for the logged-in manager's `UserId`
  - Return `View("Dashboard", new ManagerDashboardViewModel { ... })`
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [ ]* 7.1 Write property test — KPI status counts reflect the latest log entry per KPI (Property 6)
    - **Property 6: KPI status counts reflect the latest log entry per KPI**
    - Generate random KPIs with multiple log entries per KPI; assert `OnTrack + AtRisk + Behind` equals the count of KPIs that have at least one entry, and each count matches the latest-entry status
    - **Validates: Requirements 4.1, 5.1**

  - [ ]* 7.2 Write property test — Manager trend data contains exactly 6 month slots per department (Property 9)
    - **Property 9: Manager trend data contains exactly 6 month slots per department**
    - Generate random log entries across varying months and departments; assert each `TrendDatasetViewModel` has exactly 6 values and null appears only for months with no data
    - **Validates: Requirements 4.3**

  - [ ]* 7.3 Write property test — Strategic goals filter excludes Cancelled and Completed (Property 10)
    - **Property 10: Strategic goals filter excludes Cancelled and Completed**
    - Generate random `StrategicGoal` records with all possible status values; assert `ActiveGoals` contains exactly those with status not in `{"Cancelled", "Completed"}`
    - **Validates: Requirements 4.4**

- [x] 8. Add `BuildStaffDashboardAsync()` to `HomeController`
  - Read `userId` and `departmentId` from session; set `HasDepartment = departmentId != null`
  - If `HasDepartment` is false, return the ViewModel with all collections empty
  - Query `MyKpiList`: active KPIs where `DepartmentId == userDeptId`, include latest log entry, project to `IReadOnlyList<KpiRowViewModel>`
  - Derive `MyKpis`, `OnTrack`, `AtRisk`, `Behind` from `MyKpiList`
  - Compute `PendingKpiNames`: active KPIs in the user's department with no entry in the current month
  - Query `MyRecentLogs`: top 5 `KpiLogEntries` where `LoggedByUserId == userId`, include `Kpi`, project to `IReadOnlyList<RecentKpiLogViewModel>`
  - Build `Scorecard`: group `MyKpiList` by `Perspective`, project to `IReadOnlyList<ScorecardPerspectiveViewModel>`
  - Compute `UnreadNotifications` for the logged-in user's `UserId`
  - Return `View("Dashboard", new StaffDashboardViewModel { ... })`
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 8.1, 8.2_

  - [ ]* 8.1 Write property test — Staff dashboard is scoped to the user's department (Property 7)
    - **Property 7: Staff dashboard is scoped to the user's department**
    - Generate KPIs across multiple departments; assert every entry in `MyKpiList` has `DepartmentId` equal to the logged-in user's department and no KPI from another department appears
    - **Validates: Requirements 5.2, 8.1**

  - [ ]* 8.2 Write property test — Unread notification count is user-scoped (Property 15)
    - **Property 15: Unread notification count is user-scoped**
    - Generate notifications for multiple users; assert the unread count in the ViewModel equals only the unread notifications for the logged-in user
    - **Validates: Requirements 4.6, 5.6**

- [x] 9. Add `BuildExecutiveDashboardAsync()` to `HomeController`
  - Compute latest-entry-per-KPI (reuse the same `GroupBy` pattern)
  - Compute `OverallPerformancePct = Round(onTrackCount / kpisWithEntries * 100)`, returning 0 when `kpisWithEntries == 0`
  - Compute `ActiveGoals`: count of `StrategicGoals` where `Status == "In Progress"`
  - Compute `DepartmentsTracked`: count of departments that have at least one active KPI
  - Build `BscPerspectives`: group latest-entry KPIs by `Perspective`, project to `IReadOnlyList<BscPerspectiveViewModel>`
  - Set `ChartOnTrack`, `ChartAtRisk`, `ChartBehind` from the latest-entry counts
  - Build `TopDepartments`: for each department compute `ScorePct`, order descending, project to `IReadOnlyList<DeptPerformanceViewModel>`
  - Build `UnderperformingKpis`: active KPIs whose latest entry status is `"Behind"`, include department, project to `IReadOnlyList<UnderperformingKpiViewModel>`
  - Return `View("Dashboard", new ExecutiveDashboardViewModel { ... })`
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

  - [ ]* 9.1 Write property test — Overall Performance Percentage is correctly computed (Property 11)
    - **Property 11: Overall Performance Percentage is correctly computed**
    - Generate random KPI/log-entry combinations including the zero-entry edge case; assert `OverallPerformancePct == Round(onTrack / kpisWithEntries * 100)` and equals 0 when no entries exist
    - **Validates: Requirements 6.1**

  - [ ]* 9.2 Write property test — Top departments are ranked by performance score descending (Property 12)
    - **Property 12: Top departments are ranked by performance score descending**
    - Generate random department/KPI/log-entry data; assert no department in `TopDepartments` appears before a department with a strictly higher `ScorePct`
    - **Validates: Requirements 6.4**

  - [ ]* 9.3 Write property test — Underperforming KPIs list contains only Behind KPIs (Property 13)
    - **Property 13: Underperforming KPIs list contains only Behind KPIs**
    - Generate random KPIs with varying latest-entry statuses; assert every entry in `UnderperformingKpis` has latest status `"Behind"` and every `"Behind"` KPI appears in the list
    - **Validates: Requirements 6.5**

- [x] 10. Checkpoint — Ensure all controller builder methods compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Update `Dashboard.cshtml` to dispatch to role partials
  - Change `@model DashboardPageViewModel` to `@model object`
  - Replace the entire view body with the type-dispatch partial block from the design document
  - Use `@if (Model is SuperAdminDashboardViewModel) { <partial name="_SuperAdminDashboard" model="(SuperAdminDashboardViewModel)Model" /> }` pattern for all five roles
  - Remove the old summary-card loop and chart canvas that are no longer needed in the shell
  - _Requirements: 1.1–1.6_

- [x] 12. Update `_Layout.cshtml` sidebar KPI Management gate
  - Rename the local variable `isManagerOrAdmin` to `canManageKpis` in the `@{ }` block at the top of the layout
  - Update the condition to `sessionUserRole is "Admin" or "Administrator" or "Manager"`
  - Update both usages of the variable (desktop nav `@if` block and mobile nav `@if` block) to reference `canManageKpis`
  - _Requirements: 7.4, 7.5_

  - [ ]* 12.1 Write property test — Dashboard routing covers all valid roles (Property 1)
    - **Property 1: Dashboard routing covers all valid roles**
    - For each role in `{"Admin", "Administrator", "Manager", "User", "Executive"}`, assert `Dashboard()` returns a `ViewResult` whose `Model` is an instance of the expected ViewModel type and is never a redirect
    - **Validates: Requirements 1.1–1.6**

  - [ ]* 12.2 Write property test — Unknown role redirects to Login (Property 2)
    - **Property 2: Unknown role redirects to Login**
    - Generate arbitrary strings not in the five valid roles; assert `Dashboard()` returns a `RedirectToActionResult` pointing to `Login`
    - **Validates: Requirements 1.7**

- [x] 13. Create `_SuperAdminDashboard.cshtml` partial view
  - Add `@model SuperAdminDashboardViewModel` directive
  - Render four summary cards (Total Users, Total Departments, Total KPIs, Total Audit Entries) using Bootstrap card grid
  - Render System Health Snapshot section (Total Notifications, Total KPI Log Entries counts)
  - Render Recent Audit Log table (10 rows: user name, action, entity type, relative time) with empty-state placeholder per Req 9.4
  - Render User Role Distribution doughnut chart: `<canvas id="superAdminRoleChart" data-labels="..." data-datasets="...">` using `JsonSerializer.Serialize`
  - Render Department Overview table (name, user count, active KPI count)
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 9.1, 9.4_

- [x] 14. Create `_AdministratorDashboard.cshtml` partial view
  - Add `@model AdministratorDashboardViewModel` directive
  - Render four summary cards (Total Users, Total Departments, Pending KPIs, New Users This Month)
  - Render Unread Notifications count badge/card
  - Render User List Overview table (10 rows: full name, role badge, department, created date) with empty-state placeholder
  - Render Department Summary table (name, user count, active KPI count)
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 9.1_

- [x] 15. Create `_ManagerDashboard.cshtml` partial view
  - Add `@model ManagerDashboardViewModel` directive
  - Render four summary cards (Total KPIs, On Track, At Risk, Behind)
  - Render KPI Status Overview bar chart: `<canvas id="managerBarChart" data-labels="..." data-datasets="...">` with department names as labels and three datasets (On Track / At Risk / Behind)
  - Render KPI Trend line chart: `<canvas id="managerTrendChart" data-labels="..." data-datasets="...">` with one dataset per department
  - Render Strategic Goals Progress list (title, perspective, status badge, due date) with empty-state placeholder
  - Render Recent KPI Logs section (5 rows: KPI name, logged by, actual with unit, status, relative time)
  - Render Unread Notifications count
  - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 9.1_

- [x] 16. Create `_StaffDashboard.cshtml` partial view
  - Add `@model StaffDashboardViewModel` directive
  - Render four summary cards (My KPIs, On Track, At Risk, Behind)
  - Render My KPI List table (name, perspective, target, actual, status badge) with empty-state placeholder
  - Render Due for Logging section listing `PendingKpiNames`; show "All KPIs are up to date for this period." when empty (Req 9.2)
  - Render My Recent Logs section (5 rows: KPI name, actual with unit, status badge, relative time)
  - Render My Scorecard section grouped by perspective (On Track / At Risk / Behind per perspective)
  - Render Unread Notifications count
  - When `HasDepartment` is false, render empty-state placeholders for all sections
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 8.2, 9.1, 9.2_

- [x] 17. Create `_ExecutiveDashboard.cshtml` partial view
  - Add `@model ExecutiveDashboardViewModel` directive
  - Render four summary cards (Overall Performance %, Total KPIs, Active Goals, Departments Tracked)
  - Render Balanced Scorecard Summary: four perspective cards each showing On Track / At Risk / Behind counts
  - Render KPI Status Distribution doughnut chart: `<canvas id="execDoughnutChart" data-labels="..." data-datasets="...">` with On Track / At Risk / Behind values from `ChartOnTrack`, `ChartAtRisk`, `ChartBehind`
  - Render Top Performing Departments list (department name + score %) ordered descending
  - Render Underperforming KPIs section (KPI name, department, target, actual); show "No KPIs are currently behind target." when empty (Req 9.3)
  - Render link to Executive Reporting page (Req 6.6)
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 9.1, 9.3_

- [x] 18. Add Chart.js initialisation helpers to `site.js`
  - Add a generic `initChartFromCanvas(canvasId, type, options)` helper that reads `data-labels` and `data-datasets` from the canvas element and constructs a `new Chart(...)` instance
  - Add `initSuperAdminCharts()` that calls `initChartFromCanvas("superAdminRoleChart", "doughnut", { ... })` with legend at bottom
  - Add `initManagerCharts()` that calls `initChartFromCanvas` for `"managerBarChart"` (type `"bar"`, stacked axes) and `"managerTrendChart"` (type `"line"`, `spanGaps: false`, tension 0.4)
  - Add `initExecutiveCharts()` that calls `initChartFromCanvas("execDoughnutChart", "doughnut", { ... })` with legend at bottom
  - Call all three new init functions inside the existing `DOMContentLoaded` handler
  - Keep the existing `initDashboardCharts()` and `initPerformanceCharts()` functions unchanged
  - _Requirements: 2.3, 4.2, 4.3, 6.3_

- [x] 19. Checkpoint — Ensure all tests pass and all five dashboards render correctly
  - Ensure all tests pass, ask the user if questions arise.

  - [ ]* 19.1 Write property test — Role string round-trip for new roles (Property 14)
    - **Property 14: Role string round-trip for new roles**
    - Create `AppUser` records with roles `"Executive"` and `"Administrator"`, persist to in-memory DB, read back, and assert the role string is unchanged
    - **Validates: Requirements 7.2**

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Property-based tests use **FsCheck** against an in-memory EF Core database; each test runs a minimum of 100 iterations
- Each property test is tagged with a comment in the format: `// Feature: role-based-dashboards, Property N: <title>`
- Chart data is passed via `data-labels` / `data-datasets` attributes serialised with `System.Text.Json.JsonSerializer.Serialize`; no inline `<script>` blocks in partials
- The `Down()` migration must delete rows in reverse insertion order to avoid FK constraint violations
- `DashboardPageViewModel` is retained as-is; it is no longer used by `Dashboard()` but may be referenced elsewhere
