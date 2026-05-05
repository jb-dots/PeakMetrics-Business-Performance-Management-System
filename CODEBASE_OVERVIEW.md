# PeakMetrics — Codebase Overview

A plain-English guide to every concept, page, and function in this project.

---

## What Is This App?

**PeakMetrics** is a web-based **KPI (Key Performance Indicator) management system** built with ASP.NET Core MVC (.NET 8) and SQL Server. It lets an organisation:

- Track performance metrics (KPIs) across departments
- Log actual values against targets and automatically flag whether each KPI is On Track, At Risk, or Behind
- Manage strategic goals aligned to a Balanced Scorecard framework
- Give different users different views and permissions based on their role

---

## Technology Stack

| Layer | Technology |
|---|---|
| Web framework | ASP.NET Core MVC 8 |
| Database | SQL Server (via Entity Framework Core) |
| Auth | Custom session-based (BCrypt password hashing) |
| Frontend | Razor views, Bootstrap, Chart.js |
| Deployment | IIS (deploy.bat / deploy.secrets.bat) |

---

## Project Structure

```
PeakMetrics.Web/
├── Controllers/        ← All HTTP request handling (one controller: HomeController)
├── Models/             ← Database entity classes
├── ViewModels/         ← Data shapes passed from controller to views
├── Views/
│   ├── Home/           ← All page templates (.cshtml)
│   └── Shared/         ← Shared layout (_Layout.cshtml)
├── Data/
│   └── AppDbContext.cs ← EF Core database context + seed data
├── Migrations/         ← EF Core database migration history
├── wwwroot/            ← Static files (CSS, JS, images)
├── Program.cs          ← App startup and configuration
└── appsettings*.json   ← Configuration files
```

---

## Startup — Program.cs

This file boots the application. Key things it does:

1. **Registers the database** — connects to SQL Server using the `PEAKMETRICS_CONNECTION_STRING` environment variable (or `appsettings.Development.json` locally). Retries up to 3 times on failure.
2. **Registers session** — 8-hour idle timeout, HTTPS-only, HTTP-only cookie.
3. **Auto-runs migrations** — on every startup, any pending database migrations are applied automatically (safe for both dev and production).
4. **Adds security headers** — `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`.
5. **Sets the default route** — the app starts at `Home/Login` instead of the usual `Home/Index`.

---

## Database — AppDbContext.cs

The single EF Core context. It defines these tables and their relationships:

### Tables (DbSets)

| Table | Purpose |
|---|---|
| `Users` | All user accounts |
| `Departments` | Organisational departments (Finance, HR, Sales, etc.) |
| `Perspectives` | Balanced Scorecard perspectives (Financial, Customer, Internal Process, Learning & Growth) |
| `Kpis` | KPI definitions (name, target, unit, department, perspective) |
| `KpiLogEntries` | Actual values logged against a KPI for a given period |
| `GoalKpis` | Join table linking strategic goals to KPIs (many-to-many) |
| `StrategicGoals` | High-level organisational goals |
| `Notifications` | In-app alerts sent to users when a KPI goes off track |
| `AuditLogs` | Immutable record of every significant action in the system |

### Key Relationships

- A **Department** has many **Users** and many **KPIs**
- A **KPI** belongs to one **Department** and one **Perspective**
- A **KpiLogEntry** belongs to one **KPI** and was logged by one **User**
- A **StrategicGoal** belongs to one **Perspective** and can be linked to many **KPIs** (via GoalKpi)
- A **Notification** belongs to one **User** and optionally references one **KPI**
- An **AuditLog** optionally references one **User**

### Seed Data

The database is pre-populated with:
- 6 departments: Finance, HR, Sales, Operations, Customer Service, Quality
- 4 BSC perspectives: Financial, Customer, Internal Process, Learning & Growth
- 6 demo user accounts (see roles section below)
- 8 sample KPIs (one or two per department/perspective)

---

## Data Models — Models/

### AppUser
Represents a system user.
- `Id`, `FullName`, `Email`, `PasswordHash` (BCrypt), `Role`, `DepartmentId`
- `IsActive` — deactivated users cannot log in
- `LastLoginAt` — updated on every successful login
- Roles: `Super Admin`, `Administrator`, `Manager`, `Staff`, `Executive`

### Department
An organisational unit.
- `Name`, `Description`, `IsArchived`
- Has collections of `Users` and `Kpis`

### Perspective
One of the four Balanced Scorecard quadrants.
- `Name` (Financial / Customer / Internal Process / Learning & Growth)
- Has collections of `Kpis` and `StrategicGoals`

### Kpi
A single performance indicator definition.
- `Name`, `Unit` (%, score, days, hrs, etc.), `Target`, `Frequency` (Monthly/Quarterly/Annual)
- `PerspectiveId`, `DepartmentId`, `CreatedByUserId`
- `IsActive` — archived KPIs are hidden from normal views
- `Status` — a cached label; the real status is computed from the latest `KpiLogEntry`

### KpiLogEntry
A single data point logged for a KPI.
- `KpiId`, `LoggedByUserId`, `ActualValue`, `Period` (e.g. "April 2025"), `LoggedAt`
- `Status` — computed at save time: `On Track`, `At Risk`, or `Behind`
- `Notes` — optional free-text comment

### StrategicGoal
A high-level organisational objective.
- `Title`, `Description`, `PerspectiveId`, `OwnerUserId`, `TargetYear`
- `Status`: `Not Started`, `In Progress`, `Completed`, `Cancelled`
- `IsArchived` — soft-delete

### GoalKpi
Join table for the many-to-many between `StrategicGoal` and `Kpi`.
- Composite primary key: `(GoalId, KpiId)`

### Notification
An in-app alert.
- `UserId`, `Title`, `Message`, `Type` (Alert / Warning / Info), `IsRead`, `KpiId`
- Created automatically when a KPI log entry is saved with a non-"On Track" status

### AuditLog
An immutable activity record.
- `UserId`, `Action` (e.g. "Login", "Created KPI"), `EntityType`, `EntityId`, `Details`, `OccurredAt`
- Written for every significant create/update/delete and every login/logout

---

## Roles & Permissions

| Role | What they can do |
|---|---|
| **Super Admin** | Everything — sees all data, all logs, all users |
| **Administrator** | User & department management; sees logs for non-Super-Admin users; cannot manage Super Admin accounts |
| **Manager** | KPI management (create/edit/archive), strategic goals, performance analytics, trend charts |
| **Staff** | Log KPI entries for their own department; view their department's KPIs and scorecard |
| **Executive** | Read-only high-level view — overall performance %, BSC perspectives, top departments, underperforming KPIs |

Access is enforced in the controller via two helpers:
- `HasAccess(params string[] allowedRoles)` — returns true if the session role is in the list
- `CanManageKpis()` — true for Super Admin, Administrator, Manager

---

## Authentication

The app uses **custom session-based auth** (no ASP.NET Identity).

### How login works
1. User submits email + password via `POST /Home/Login`
2. Controller looks up the user by email, checks `IsActive`
3. BCrypt verifies the password against the stored hash
4. On success: stores `UserId`, `UserName`, `UserRole`, `UserInitials` in the session
5. Updates `LastLoginAt` and writes an AuditLog entry
6. Redirects to `/Home/Dashboard`

### Auth guard
`OnActionExecutionAsync` runs before every controller action. It:
- Skips the check for `Login` and `Logout` actions
- Redirects to Login if no session exists
- Populates `ViewData` with the current user's name, role, and initials
- Loads the top 5 unread notifications for the header bell icon

### Logout
`POST /Home/Logout` clears the session and writes an AuditLog entry.

---

## The Controller — HomeController.cs

All application logic lives in a single controller. Here is every action:

### Auth Actions

| Action | Method | URL | Description |
|---|---|---|---|
| `Index` | GET | `/` | Redirects to Login |
| `Login` | GET | `/Home/Login` | Shows the login form |
| `Login` | POST | `/Home/Login` | Validates credentials, starts session |
| `Logout` | POST | `/Home/Logout` | Clears session, logs the event |

### Dashboard

| Action | Method | URL | Description |
|---|---|---|---|
| `Dashboard` | GET | `/Home/Dashboard` | Reads the session role and delegates to the correct dashboard builder |

The dashboard is role-specific. Each builder queries the database and returns a typed ViewModel:

- **`BuildSuperAdminDashboardAsync`** — total counts (users, departments, KPIs, audit entries, notifications, log entries), recent audit logs, role distribution chart, department overview
- **`BuildAdminDashboardAsync`** — similar counts, KPI status summary, pending KPIs (no entry this month), recent users, role distribution, system logs (excluding Super Admin activity)
- **`BuildManagerDashboardAsync`** — KPI status counts, per-department KPI status breakdown, 6-month trend chart (by department), active strategic goals, recent KPI logs
- **`BuildStaffDashboardAsync`** — only the logged-in user's department KPIs, pending KPI names, their own recent log entries, scorecard grouped by perspective
- **`BuildExecutiveDashboardAsync`** — overall performance %, BSC perspective breakdown, top departments by score, underperforming KPIs list

### KPI Tracking

| Action | Method | URL | Description |
|---|---|---|---|
| `KPITracking` | GET | `/Home/KPITracking` | Lists all KPIs with filters (department, perspective, status, archived toggle) |
| `KpiDetail` | GET | `/Home/KpiDetail/{id}` | Returns KPI details as JSON (used by the log entry form) |

### KPI Log Entry

| Action | Method | URL | Description |
|---|---|---|---|
| `KPILogEntry` | GET | `/Home/KPILogEntry` | Shows the log entry form (optionally pre-selects a KPI via `?kpiId=`) |
| `KPILogEntry` | POST | `/Home/KPILogEntry` | Saves the entry, computes status, creates a notification if off-track, writes audit log |

### KPI Management (CRUD)

| Action | Method | URL | Description |
|---|---|---|---|
| `KpiManagement` | GET | `/Home/KpiManagement` | Redirects to KPITracking (consolidated view) |
| `KpiCreate` | GET | `/Home/KpiCreate` | Shows the KPI creation form |
| `KpiCreate` | POST | `/Home/KpiCreate` | Saves a new KPI |
| `KpiEdit` | GET | `/Home/KpiEdit/{id}` | Shows the edit form pre-filled |
| `KpiEdit` | POST | `/Home/KpiEdit/{id}` | Saves changes, logs target changes in audit |
| `KpiToggleActive` | POST | `/Home/KpiToggleActive/{id}` | Toggles IsActive (archive/restore) |

### Strategic Planning

| Action | Method | URL | Description |
|---|---|---|---|
| `StrategicPlanning` | GET | `/Home/StrategicPlanning` | Lists active (or archived) strategic goals |
| `StrategicGoalCreate` | GET | `/Home/StrategicGoalCreate` | Shows the goal creation form |
| `StrategicGoalCreate` | POST | `/Home/StrategicGoalCreate` | Saves a new goal |
| `StrategicGoalEdit` | GET | `/Home/StrategicGoalEdit/{id}` | Shows the edit form |
| `StrategicGoalEdit` | POST | `/Home/StrategicGoalEdit/{id}` | Saves changes |
| `StrategicGoalArchive` | POST | `/Home/StrategicGoalArchive/{id}` | Toggles IsArchived (archive/restore) |

### Reporting & Analytics

| Action | Method | URL | Description |
|---|---|---|---|
| `BalancedScorecard` | GET | `/Home/BalancedScorecard` | Shows all active KPIs grouped by the 4 BSC perspectives with latest actual vs target |
| `PerformanceAnalytics` | GET | `/Home/PerformanceAnalytics` | Trend line chart (by perspective), bar chart (department scores), doughnut (status split). Filterable by department and time range (3/6/12 months) |
| `ExecutiveReporting` | GET | `/Home/ExecutiveReporting` | Full KPI table with variance, scorecard by perspective, strategic goals list |

### User & Department Management

| Action | Method | URL | Description |
|---|---|---|---|
| `UserManagement` | GET | `/Home/UserManagement` | Lists all users |
| `UserCreate` | GET/POST | `/Home/UserCreate` | Create a new user (Super Admin role cannot be assigned here) |
| `UserEdit` | GET/POST | `/Home/UserEdit/{id}` | Edit a user; Administrator cannot edit Super Admin accounts |
| `UserToggleActive` | POST | `/Home/UserToggleActive/{id}` | Activate/deactivate a user |
| `DepartmentManagement` | GET | `/Home/DepartmentManagement` | Lists departments (active or archived) |
| `DepartmentCreate` | GET/POST | `/Home/DepartmentCreate` | Create a department |
| `DepartmentEdit` | GET/POST | `/Home/DepartmentEdit/{id}` | Edit a department |
| `DepartmentDelete` | POST | `/Home/DepartmentDelete/{id}` | Hard-delete (blocked if users or KPIs are assigned) |
| `DepartmentArchive` | POST | `/Home/DepartmentArchive/{id}` | Soft-archive/restore |

### Other Pages

| Action | Method | URL | Description |
|---|---|---|---|
| `Notifications` | GET | `/Home/Notifications` | Full notification list for the logged-in user |
| `Profile` | GET/POST | `/Home/Profile` | View and update own name, email, and password |
| `SystemLogs` | GET | `/Home/SystemLogs` | Audit log viewer (last 200 entries; Super Admin sees all, Administrator sees non-Super-Admin) |
| `AccessDenied` | GET | `/Home/AccessDenied` | Shown when a user hits a forbidden route |

---

## Key Business Logic

### Status Computation — `ComputeStatus(kpi, actual)`

Called every time a KPI log entry is saved. Determines whether the actual value is On Track, At Risk, or Behind.

**For "lower is better" KPIs** (unit = "days", or name contains "Turnover", "Defect", "Cycle"):
- `actual <= target` → **On Track**
- `actual <= target × 1.25` → **At Risk**
- otherwise → **Behind**

**For "higher is better" KPIs** (everything else):
- `actual >= target` → **On Track**
- `actual >= target × 0.85` → **At Risk**
- otherwise → **Behind**

### Automatic Notifications

When a KPI log entry is saved and the computed status is **not** On Track, a `Notification` is automatically created for the user who logged it:
- Status = Behind → Type = "Alert"
- Status = At Risk → Type = "Warning"

### Trend Chart Data — `BuildTrendDatasets`

Groups log entries by (department, year, month) and averages the actual values. Returns one dataset per department, with `null` for months that have no data (Chart.js skips nulls).

### Latest Entry Per KPI — `GetLatestEntriesPerKpiAsync`

Used everywhere a "current status" is needed. Groups all log entries by KPI and picks the most recent one (by `LoggedAt`, then by `Id` as a tiebreaker).

---

## Views — Pages

### Shared Layout (`Views/Shared/_Layout.cshtml`)
The master template used by all pages. Contains:
- Top navigation bar with user name, initials, notification bell, and logout button
- Sidebar navigation (links vary by role)
- Flash message area (success/error TempData)
- Chart.js and Bootstrap script includes

### Dashboard (`Views/Home/Dashboard.cshtml`)
A thin shell that renders one of five role-specific partial views:
- `_SuperAdminDashboard.cshtml`
- `_AdministratorDashboard.cshtml`
- `_ManagerDashboard.cshtml`
- `_StaffDashboard.cshtml`
- `_ExecutiveDashboard.cshtml`

### Login (`Views/Home/Login.cshtml`)
Email + password form. No layout chrome — standalone page.

### KPI Tracking (`Views/Home/KPITracking.cshtml`)
Filterable table of all KPIs. Each row shows name, department, perspective, target, actual, and status badge. Links to Edit, Log Entry, and Archive actions.

### KPI Log Entry (`Views/Home/KPILogEntry.cshtml`)
Form to record an actual value for a KPI. Selecting a KPI auto-fills its target and unit via a fetch to `KpiDetail`. Fields: KPI selector, actual value, period, date, notes.

### KPI Form (`Views/Home/KpiForm.cshtml`)
Shared create/edit form for KPIs. Fields: name, department, perspective, unit, target, description, active toggle.

### Balanced Scorecard (`Views/Home/BalancedScorecard.cshtml`)
Four-quadrant table (one section per BSC perspective) showing each KPI's target, actual, and status.

### Performance Analytics (`Views/Home/PerformanceAnalytics.cshtml`)
Three charts:
1. Line chart — average actual value per perspective over time
2. Bar chart — % On Track per department
3. Doughnut chart — overall On Track / At Risk / Behind split

### Strategic Planning (`Views/Home/StrategicPlanning.cshtml`)
Card grid of strategic goals. Each card shows title, perspective, status badge, target year, and owner. Buttons to edit or archive.

### Strategic Goal Form (`Views/Home/StrategicGoalForm.cshtml`)
Create/edit form for strategic goals. Fields: title, description, perspective, status, target year.

### Executive Reporting (`Views/Home/ExecutiveReporting.cshtml`)
Full KPI table with variance column, scorecard summary by perspective, and strategic goals list.

### User Management (`Views/Home/UserManagement.cshtml`)
Table of all users with role, department, status, and last login. Buttons to edit or toggle active.

### User Form (`Views/Home/UserForm.cshtml`)
Create/edit form for users. Fields: full name, email, password (optional on edit), role, department, active toggle.

### Department Management (`Views/Home/DepartmentManagement.cshtml`)
Table of departments with user count and KPI count. Buttons to edit, archive, or delete (delete blocked if in use).

### Department Form (`Views/Home/DepartmentForm.cshtml`)
Create/edit form for departments. Fields: name, description.

### Notifications (`Views/Home/Notifications.cshtml`)
Full list of the current user's notifications, ordered newest first, with read/unread styling.

### Profile (`Views/Home/Profile.cshtml`)
Shows the current user's info and provides a form to update name, email, and password.

### System Logs (`Views/Home/SystemLogs.cshtml`)
Table of the last 200 audit log entries: timestamp, user, action, entity type, details.

### Access Denied (`Views/Home/AccessDenied.cshtml`)
Simple error page shown when a user tries to access a page their role doesn't permit.

---

## ViewModels — ViewModels/

ViewModels are plain C# classes that carry exactly the data a view needs. Key ones:

| ViewModel | Used by |
|---|---|
| `LoginViewModel` | Login form |
| `SuperAdminDashboardViewModel` | Super Admin dashboard |
| `AdministratorDashboardViewModel` | Administrator dashboard |
| `ManagerDashboardViewModel` | Manager dashboard |
| `StaffDashboardViewModel` | Staff dashboard |
| `ExecutiveDashboardViewModel` | Executive dashboard |
| `KpiTrackingPageViewModel` / `KpiTrackingItemViewModel` | KPI Tracking page |
| `KpiFormViewModel` | KPI create/edit form |
| `KpiLogEntryViewModel` | KPI log entry form |
| `BalancedScorecardViewModel` | Balanced Scorecard page |
| `PerformanceAnalyticsViewModel` | Performance Analytics page |
| `StrategicPlanningViewModel` / `StrategicGoalCardViewModel` | Strategic Planning page |
| `ExecutiveReportingViewModel` | Executive Reporting page |
| `UserManagementListViewModel` / `UserFormViewModel` | User Management pages |
| `DepartmentManagementViewModel` / `DepartmentFormViewModel` | Department Management pages |
| `NotificationsPageViewModel` / `NotificationItemViewModel` | Notifications page |
| `ProfileViewModel` | Profile page |
| `TrendDatasetViewModel` | Chart data (line charts) |
| `AuditLogEntryViewModel` | System Logs page |
| `AlertSeverity` (enum) | Standard / Warning / Critical — used for badge colours |

---

## Configuration Files

| File | Purpose |
|---|---|
| `appsettings.json` | Base config (logging, allowed hosts) |
| `appsettings.Development.json` | Local dev overrides — contains the local DB connection string |
| `appsettings.Production.json` | Production overrides — connection string comes from environment variable, not this file |
| `web.config` | IIS hosting configuration |

---

## Deployment

- `deploy.bat` — builds the project in Release mode and deploys to IIS via Web Deploy
- `deploy.secrets.bat` — same but injects the production connection string and credentials (not committed to source control)
- `deploy.secrets.example.bat` — template showing what variables `deploy.secrets.bat` needs

---

## Migrations

EF Core migration files in `Migrations/` track every schema change in order:

| Migration | What it did |
|---|---|
| `20260423165446_InitialCreate` | Created all base tables |
| `20260423191508_SeedRealAccounts` | Added real seed user accounts |
| `20260423220637_SeedExecutiveAndAdministrator` | Added Executive and Administrator seed accounts |
| `20260425171837_AddStrategicGoalArchive` | Added archive support to StrategicGoal |
| `20260425171905_AddIsArchivedToGoal` | Added `IsArchived` column to StrategicGoal |
| `20260425171917_GoalArchive` | Consolidated goal archive changes |
| `20260427064205_DepartmentArchive` | Added `IsArchived` to Department |
| `20260501180822_ErdAlignment` | Aligned schema to ERD (PerspectiveId FK, etc.) |
| `20260504054914_UpdateSeedEmails` | Updated seed account email addresses |

---

## How Data Flows — A Typical Request

**Example: Staff user logs a KPI value**

1. User navigates to `/Home/KPILogEntry`
2. `OnActionExecutionAsync` checks session → valid, populates ViewData
3. `KPILogEntry GET` loads the KPI dropdown and returns the form view
4. User selects a KPI → JavaScript fetches `/Home/KpiDetail/{id}` → auto-fills target/unit
5. User fills in actual value, period, notes and submits
6. `KPILogEntry POST` validates the form
7. Looks up the KPI from the database
8. Calls `ComputeStatus(kpi, actualValue)` → e.g. "At Risk"
9. Saves a new `KpiLogEntry` row
10. Because status ≠ "On Track", saves a new `Notification` row (Type = "Warning")
11. Saves a new `AuditLog` row
12. Redirects to `/Home/KPITracking` with a success flash message
