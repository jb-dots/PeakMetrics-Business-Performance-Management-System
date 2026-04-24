# Requirements Document

## Introduction

PeakMetrics is an ASP.NET Core 8 MVC business performance management (BPM) system. Currently all authenticated users land on the same generic dashboard regardless of their role. This feature replaces that single dashboard with five distinct, role-tailored dashboards so that each user immediately sees the information most relevant to their responsibilities.

The system currently has three session roles: **Admin**, **Manager**, and **User**. The user's breakdown describes five conceptual dashboards. To support all five without a database migration, a new **Executive** role is introduced and the existing **Admin** role is split into **Admin** (Super Admin — system control) and **Administrator** (people & structure manager). The mapping is:

| Session Role | Dashboard Rendered |
|---|---|
| `Admin` | Super Admin Dashboard |
| `Administrator` | Administrator Dashboard |
| `Manager` | Manager Dashboard |
| `User` | Staff/Employee Dashboard |
| `Executive` | Executive Dashboard |

The `HomeController.Dashboard` action reads the session role and returns the appropriate partial view or view model. All data is sourced from the existing EF Core models: `AppUser`, `Department`, `Kpi`, `KpiLogEntry`, `Notification`, `AuditLog`, and `StrategicGoal`.

---

## Glossary

- **Dashboard_Router**: The component within `HomeController` responsible for reading the session role and dispatching to the correct dashboard view model builder.
- **Super_Admin_Dashboard**: The dashboard rendered for users with role `Admin`. Theme: "System Control Panel".
- **Administrator_Dashboard**: The dashboard rendered for users with role `Administrator`. Theme: "People and Structure Manager".
- **Manager_Dashboard**: The dashboard rendered for users with role `Manager`. Theme: "Performance Command Center".
- **Staff_Dashboard**: The dashboard rendered for users with role `User`. Theme: "My Personal Performance Board".
- **Executive_Dashboard**: The dashboard rendered for users with role `Executive`. Theme: "Company Health at a Glance".
- **KPI_Status**: One of three string values — `On Track`, `At Risk`, or `Behind` — derived from the most recent `KpiLogEntry` for a given `Kpi`.
- **Current_Period**: The most recent calendar period (month or quarter) for which at least one `KpiLogEntry` exists in the database.
- **Pending_KPI**: An active `Kpi` that has no `KpiLogEntry` recorded in the Current_Period.
- **Balanced_Scorecard_Perspective**: One of four standard BSC categories: `Financial`, `Customer`, `Internal Process`, `Learning & Growth`.
- **Overall_Performance_Percentage**: The ratio of KPIs with KPI_Status `On Track` to total active KPIs with at least one log entry, expressed as a percentage rounded to the nearest whole number.
- **Department_Performance_Score**: The percentage of a department's active KPIs that are `On Track`, used to rank departments.

---

## Requirements

### Requirement 1: Role-Based Dashboard Routing

**User Story:** As any authenticated user, I want to be automatically shown the dashboard designed for my role, so that I only see information relevant to my responsibilities.

#### Acceptance Criteria

1. WHEN an authenticated user navigates to the `Dashboard` action, THE Dashboard_Router SHALL read the session role value and select the corresponding dashboard view model builder.
2. WHEN the session role is `Admin`, THE Dashboard_Router SHALL build and return the Super_Admin_Dashboard view.
3. WHEN the session role is `Administrator`, THE Dashboard_Router SHALL build and return the Administrator_Dashboard view.
4. WHEN the session role is `Manager`, THE Dashboard_Router SHALL build and return the Manager_Dashboard view.
5. WHEN the session role is `User`, THE Dashboard_Router SHALL build and return the Staff_Dashboard view.
6. WHEN the session role is `Executive`, THE Dashboard_Router SHALL build and return the Executive_Dashboard view.
7. IF the session role does not match any of the five defined roles, THEN THE Dashboard_Router SHALL redirect the user to the Login page.

---

### Requirement 2: Super Admin Dashboard

**User Story:** As a Super Admin, I want a system control panel dashboard, so that I can monitor who is using the system and what they are doing.

#### Acceptance Criteria

1. THE Super_Admin_Dashboard SHALL display four summary cards: Total Users (count of all `AppUser` records), Total Departments (count of all `Department` records), Total KPIs (count of all active `Kpi` records), and Total Audit Entries (count of all `AuditLog` records).
2. THE Super_Admin_Dashboard SHALL display a Recent Audit Log table showing the 10 most recent `AuditLog` entries, ordered by `OccurredAt` descending, including the user's full name, action, entity type, and relative timestamp.
3. THE Super_Admin_Dashboard SHALL display a User Role Distribution doughnut chart showing the count of `AppUser` records grouped by `Role`.
4. THE Super_Admin_Dashboard SHALL display a Department Overview table listing each `Department` with its user count and active KPI count.
5. THE Super_Admin_Dashboard SHALL display a System Health Snapshot section showing the total count of `AuditLog` records, total count of `Notification` records, and total count of `KpiLogEntry` records.
6. THE Super_Admin_Dashboard SHALL NOT display KPI performance metrics, business revenue data, scorecard results, or strategic goal progress.

---

### Requirement 3: Administrator Dashboard

**User Story:** As an Administrator, I want a people and structure management dashboard, so that I can oversee users and the organisational structure.

#### Acceptance Criteria

1. THE Administrator_Dashboard SHALL display four summary cards: Total Users, Total Departments, Pending KPIs (count of active KPIs with no log entry in the Current_Period), and New Users This Month (count of `AppUser` records with `CreatedAt` in the current calendar month).
2. THE Administrator_Dashboard SHALL display a User List Overview table showing the 10 most recently created `AppUser` records, including full name, role badge, department name, and `CreatedAt` date.
3. THE Administrator_Dashboard SHALL display a Department Summary table listing each `Department` with its user count and active KPI count.
4. THE Administrator_Dashboard SHALL display a Recently Added Users section showing the 5 most recently created `AppUser` records with a role badge.
5. THE Administrator_Dashboard SHALL display the total count of unread `Notification` records across all users.
6. THE Administrator_Dashboard SHALL NOT display KPI performance data, analytics charts, strategic goals, executive reports, or audit log entries.

---

### Requirement 4: Manager Dashboard

**User Story:** As a Manager, I want a performance command centre dashboard, so that I can monitor how the company is performing against its KPIs and strategic goals.

#### Acceptance Criteria

1. THE Manager_Dashboard SHALL display four summary cards: Total KPIs (count of all active `Kpi` records), On Track count, At Risk count, and Behind count — each derived from the latest `KpiLogEntry` per KPI.
2. THE Manager_Dashboard SHALL display a KPI Status Overview bar chart comparing the count of `On Track`, `At Risk`, and `Behind` KPIs grouped by department name.
3. THE Manager_Dashboard SHALL display a KPI Trend line chart showing the average `ActualValue` of all KPI log entries grouped by month for the 6 most recent calendar months.
4. THE Manager_Dashboard SHALL display a Strategic Goals Progress list showing all active `StrategicGoal` records (status not `Cancelled` and not `Completed`) with title, perspective, status badge, and due date.
5. THE Manager_Dashboard SHALL display a Recent KPI Logs section showing the 5 most recent `KpiLogEntry` records, including KPI name, the full name of the user who logged it, the actual value with unit, and a relative timestamp.
6. THE Manager_Dashboard SHALL display the count of unread `Notification` records for the currently logged-in Manager user.
7. THE Manager_Dashboard SHALL NOT display user management data, department management forms, audit log entries, or system settings.

---

### Requirement 5: Staff/Employee Dashboard

**User Story:** As a Staff member, I want a personal performance board dashboard, so that I can see my assigned KPIs and know whether I need to log any data today.

#### Acceptance Criteria

1. THE Staff_Dashboard SHALL display four summary cards: My KPIs (count of active `Kpi` records in the same department as the logged-in user), On Track count, At Risk count, and Behind count — each derived from the latest `KpiLogEntry` for those KPIs.
2. THE Staff_Dashboard SHALL display a My KPI List table showing all active `Kpi` records assigned to the logged-in user's department, including KPI name, perspective, target with unit, last logged actual value with unit, and KPI_Status badge.
3. THE Staff_Dashboard SHALL display a Due for Logging section listing Pending_KPIs within the logged-in user's department.
4. THE Staff_Dashboard SHALL display a My Recent Logs section showing the 5 most recent `KpiLogEntry` records submitted by the logged-in user, including KPI name, actual value with unit, status badge, and relative timestamp.
5. THE Staff_Dashboard SHALL display a My Scorecard section grouping the logged-in user's department KPIs by Balanced_Scorecard_Perspective, showing the count of `On Track`, `At Risk`, and `Behind` KPIs per perspective.
6. THE Staff_Dashboard SHALL display the count of unread `Notification` records for the currently logged-in user.
7. THE Staff_Dashboard SHALL NOT display KPI data for other departments, company-wide analytics, strategic goals, executive reports, audit log entries, or user and department management data.

---

### Requirement 6: Executive Dashboard

**User Story:** As an Executive, I want a company health at a glance dashboard, so that I can quickly assess overall organisational performance without operational detail.

#### Acceptance Criteria

1. THE Executive_Dashboard SHALL display four summary cards: Overall Performance (Overall_Performance_Percentage), Total KPIs (count of all active `Kpi` records), Active Goals (count of `StrategicGoal` records with status `In Progress`), and Departments Tracked (count of `Department` records that have at least one active `Kpi`).
2. THE Executive_Dashboard SHALL display a Balanced Scorecard Summary section with four perspective cards — one per Balanced_Scorecard_Perspective — each showing the count of `On Track`, `At Risk`, and `Behind` KPIs for that perspective.
3. THE Executive_Dashboard SHALL display a KPI Status Distribution doughnut chart showing the count of KPIs in each KPI_Status category across all active KPIs with at least one log entry.
4. THE Executive_Dashboard SHALL display a Top Performing Departments section listing all departments ranked in descending order by Department_Performance_Score, showing the department name and score as a percentage.
5. THE Executive_Dashboard SHALL display an Underperforming KPIs section listing all active KPIs whose latest KPI_Status is `Behind`, including KPI name, department name, target with unit, and last logged actual value with unit.
6. THE Executive_Dashboard SHALL display a link to the Executive Reporting page.
7. THE Executive_Dashboard SHALL NOT display individual staff performance data, KPI log entry forms, strategic planning forms, user management data, department management data, or audit log entries.

---

### Requirement 7: Role Expansion — Executive and Administrator Roles

**User Story:** As a system administrator, I want the system to support `Executive` and `Administrator` as distinct session roles, so that the five dashboard types can be independently assigned to users.

#### Acceptance Criteria

1. THE Dashboard_Router SHALL recognise `Executive` and `Administrator` as valid role values in addition to the existing `Admin`, `Manager`, and `User` roles.
2. WHEN a new user is created with role `Executive` or `Administrator`, THE System SHALL store the role string in `AppUser.Role` without requiring a database schema migration.
3. THE System SHALL display the correct role label in the sidebar user info section for all five role values.
4. WHEN the session role is `Admin` or `Administrator`, THE System SHALL grant access to the KPI Management navigation link.
5. WHEN the session role is `Executive`, THE System SHALL NOT display the KPI Management navigation link.

---

### Requirement 8: Dashboard Data Isolation

**User Story:** As any user, I want to be certain that my dashboard only shows data I am authorised to see, so that sensitive information is not exposed across role boundaries.

#### Acceptance Criteria

1. WHEN the Staff_Dashboard is rendered, THE Dashboard_Router SHALL scope all KPI and log entry queries to the logged-in user's `DepartmentId`.
2. WHEN the Staff_Dashboard is rendered for a user with no `DepartmentId`, THE Staff_Dashboard SHALL display an empty state message for each data section rather than showing data from other departments.
3. WHEN the Manager_Dashboard or Executive_Dashboard is rendered, THE Dashboard_Router SHALL query KPI data across all departments without filtering by the logged-in user's department.
4. WHEN the Super_Admin_Dashboard or Administrator_Dashboard is rendered, THE Dashboard_Router SHALL NOT include KPI performance status data in the view model.

---

### Requirement 9: Empty State Handling

**User Story:** As any user, I want to see a helpful message when a dashboard section has no data, so that I understand the system is working correctly and know what action to take.

#### Acceptance Criteria

1. WHEN a dashboard chart or table section has no data to display, THE Dashboard SHALL render an empty state placeholder with a descriptive icon and a short explanatory message.
2. WHEN the Due for Logging section on the Staff_Dashboard has no Pending_KPIs, THE Staff_Dashboard SHALL display the message "All KPIs are up to date for this period."
3. WHEN the Underperforming KPIs section on the Executive_Dashboard has no `Behind` KPIs, THE Executive_Dashboard SHALL display the message "No KPIs are currently behind target."
4. WHEN the Recent Audit Log on the Super_Admin_Dashboard has no entries, THE Super_Admin_Dashboard SHALL display the message "No audit activity recorded yet."
