# PeakMetrics — Business Performance Management System

**Student:** John Benedic F. Dutaro
**Subject:** IT16 — Information Assurance and Security 1
**Year:** 2026

---

## What Is PeakMetrics?

PeakMetrics is a web-based **KPI (Key Performance Indicator) and Balanced Scorecard management system** built with ASP.NET Core MVC (.NET 8) and SQL Server. It enables an organisation to:

- Track performance metrics across departments using the Balanced Scorecard (BSC) framework
- Log actual values against targets and automatically flag each KPI as **On Track**, **At Risk**, or **Behind**
- Manage strategic goals aligned to the four BSC perspectives
- Enforce role-based access so each user sees only what their role permits
- Maintain a full audit trail of every significant action in the system

---

## Technology Stack

| Layer | Technology |
|---|---|
| Web framework | ASP.NET Core MVC 8 |
| Database | SQL Server (via Entity Framework Core 8) |
| Authentication | Custom session-based auth (BCrypt password hashing) |
| Frontend | Razor Views, Bootstrap 5, Chart.js |
| Email | MailKit (SMTP) |
| Deployment | IIS via Web Deploy |
| Testing | xUnit, Moq, FluentAssertions, EF Core InMemory |

---

## Project Structure

```
PeakMetrics-Business-Performance-Management-System/
├── Controllers/
│   ├── HomeController.cs        ← All main app logic (login, dashboard, KPIs, etc.)
│   ├── AccountController.cs     ← Registration, email confirmation, password reset
│   ├── ApiController.cs         ← REST API endpoints
│   └── LandingController.cs     ← Public landing page
├── Models/                      ← EF Core entity classes
├── ViewModels/                  ← Data shapes passed to Razor views
├── Views/
│   ├── Home/                    ← All main page templates
│   ├── Account/                 ← Registration, login, password reset views
│   └── Shared/                  ← Shared layout and partials
├── Data/
│   └── AppDbContext.cs          ← EF Core DbContext + seed data
├── Migrations/                  ← EF Core migration history
├── Services/
│   ├── EmailService.cs          ← MailKit SMTP email sending
│   ├── EmailValidator.cs        ← Domain blocking and format validation
│   └── MarkdownService.cs       ← Markdown rendering helper
├── PeakMetrics.Tests/           ← xUnit test project
│   ├── AuthenticationTests.cs
│   ├── RegistrationTests.cs
│   ├── KPITests.cs
│   ├── SecurityTests.cs
│   └── Helpers/
│       ├── TestDbContextFactory.cs
│       └── KpiStatusHelper.cs
├── wwwroot/                     ← Static files (CSS, JS, images)
├── Program.cs                   ← App startup and DI configuration
└── appsettings.json             ← Configuration
```

---

## Entity Relationship Diagram (ERD)

```
┌─────────────────┐       ┌──────────────────┐
│   Department    │       │   Perspective    │
│─────────────────│       │──────────────────│
│ Id (PK)         │       │ Id (PK)          │
│ Name            │       │ Name             │
│ Description     │       └────────┬─────────┘
│ IsArchived      │                │ 1
│ CreatedAt       │                │
└────────┬────────┘                │ N
         │ 1                ┌──────┴──────────┐
         │                  │   StrategicGoal │
         │ N                │─────────────────│
┌────────┴────────┐         │ Id (PK)         │
│    AppUser      │         │ Title           │
│─────────────────│         │ Description     │
│ Id (PK)         │         │ PerspectiveId(FK)│
│ FullName        │         │ Status          │
│ Email (UNIQUE)  │         │ TargetYear      │
│ PasswordHash    │         │ OwnerUserId(FK) │
│ Role            │         │ IsArchived      │
│ DepartmentId(FK)│         │ CreatedAt       │
│ IsActive        │         └────────┬────────┘
│ IsApproved      │                  │ M
│ EmailConfirmed  │                  │
│ FailedLoginAttempts│               │ (via GoalKpi)
│ LockoutEnd      │                  │ N
│ ConfirmationToken│        ┌────────┴────────┐
│ PasswordResetToken│       │      Kpi        │
│ CreatedAt       │         │─────────────────│
│ LastLoginAt     │         │ Id (PK)         │
└────────┬────────┘         │ Name            │
         │ 1                │ PerspectiveId(FK)│
         │                  │ DepartmentId(FK)│
         │ N                │ Unit            │
┌────────┴────────┐         │ Target          │
│  KpiLogEntry   │         │ Frequency       │
│─────────────────│         │ Status          │
│ Id (PK)         │         │ IsActive        │
│ KpiId (FK)      │◄────────│ CreatedByUserId │
│ LoggedByUserId(FK)│       │ CreatedAt       │
│ ActualValue     │         └─────────────────┘
│ Status          │
│ Period          │         ┌─────────────────┐
│ Notes           │         │    GoalKpi      │
│ LoggedAt        │         │─────────────────│
└─────────────────┘         │ GoalId (PK,FK)  │
                            │ KpiId (PK,FK)   │
┌─────────────────┐         └─────────────────┘
│  Notification   │
│─────────────────│         ┌─────────────────┐
│ Id (PK)         │         │    AuditLog     │
│ UserId (FK)     │         │─────────────────│
│ KpiId (FK)      │         │ Id (PK)         │
│ Title           │         │ UserId (FK)     │
│ Message         │         │ Action          │
│ Type            │         │ EntityType      │
│ IsRead          │         │ EntityId        │
│ CreatedAt       │         │ Details         │
└─────────────────┘         │ IpAddress       │
                            │ OccurredAt      │
                            └─────────────────┘
```

### Relationships Summary

| Relationship | Type | Description |
|---|---|---|
| Department → AppUser | 1:N | A department has many users |
| Department → Kpi | 1:N | A department owns many KPIs |
| Perspective → Kpi | 1:N | A perspective groups many KPIs |
| Perspective → StrategicGoal | 1:N | A perspective groups many goals |
| Kpi ↔ StrategicGoal | M:N | Via GoalKpi join table |
| Kpi → KpiLogEntry | 1:N | A KPI has many log entries |
| AppUser → KpiLogEntry | 1:N | A user logs many entries |
| AppUser → Notification | 1:N | A user receives many notifications |
| AppUser → AuditLog | 1:N | A user generates many audit records |
| StrategicGoal → AppUser (Owner) | N:1 | A goal has one owner |

---

## Database Tables

| Table | Rows (seed) | Purpose |
|---|---|---|
| `Users` | 6 | All user accounts with roles and auth state |
| `Departments` | 6 | Organisational units (Finance, HR, Sales, etc.) |
| `Perspectives` | 4 | BSC quadrants (Financial, Customer, Internal Process, Learning & Growth) |
| `Kpis` | 8 | KPI definitions with targets and units |
| `KpiLogEntries` | 0 | Actual values logged against KPIs |
| `GoalKpis` | 0 | Many-to-many join between goals and KPIs |
| `StrategicGoals` | 0 | High-level organisational objectives |
| `Notifications` | 0 | In-app alerts for off-track KPIs |
| `AuditLogs` | 0 | Immutable activity records |

---

## Roles and Permissions

| Role | Dashboard | KPI Management | User Management | Audit Log | Strategic Goals |
|---|---|---|---|---|---|
| **Super Admin** | Full system overview | ✅ Full | ✅ Full (all roles) | ✅ All entries | ✅ Full |
| **Administrator** | Admin overview | ✅ Full | ✅ (excl. Super Admin) | ✅ Non-Super-Admin | ✅ Full |
| **Manager** | KPI trends + goals | ✅ Create/Edit/Archive | ❌ | ❌ | ✅ Create/Edit |
| **Staff** | Own dept KPIs | ✅ Log entries only | ❌ | ❌ | 👁 View only |
| **Executive** | High-level read-only | ❌ | ❌ | ❌ | 👁 View only |

---

## Core Business Processes

### 1. User Registration and Approval Flow

```
User fills registration form
        ↓
Email domain validation (blocked: peakmetrics.com, test.com, mailinator.com, etc.)
        ↓
Duplicate email check
        ↓
Account created (IsApproved=false, EmailConfirmed=false)
        ↓
Verification email sent with confirmation link
        ↓
User clicks link → EmailConfirmed=true
        ↓
Admin notified → Admin approves/rejects from Pending Users page
        ↓
IsApproved=true → User can now log in
```

### 2. Login and Session Flow

```
User submits email + password
        ↓
Look up user by email (IsActive=true required)
        ↓
BCrypt.Verify(password, hash)
        ↓ (fail)
Increment FailedLoginAttempts
If ≥ 5 → set LockoutEnd = now + 15 min, write AuditLog
        ↓ (pass)
Check LockoutEnd (if set and in future → reject)
        ↓
Check EmailConfirmed (false → reject)
        ↓
Check IsApproved (false → reject)
        ↓
Set session: UserId, UserName, UserRole, UserInitials
Update LastLoginAt, reset FailedLoginAttempts
Write AuditLog "Login"
        ↓
Redirect to /Home/Dashboard
```

### 3. KPI Status Computation

Every time a KPI log entry is saved, the status is computed automatically:

**Higher-is-better KPIs** (default — %, score, etc.):
```
actual >= target          → On Track
actual >= target × 0.75   → At Risk
actual <  target × 0.75   → Behind
```

**Lower-is-better KPIs** (unit = "days", or name contains "Turnover", "Defect", "Cycle"):
```
actual <= target          → On Track
actual <= target × 1.25   → At Risk
actual >  target × 1.25   → Behind
```

### 4. Automatic Notification Creation

When a KPI log entry is saved with a non-On-Track status:
- **Behind** → Notification Type = "Alert"
- **At Risk** → Notification Type = "Warning"

The notification is created for the user who logged the entry and linked to the KPI.

### 5. Password Reset Flow

```
User submits email on Forgot Password page
        ↓
Look up user (anti-enumeration: always show success message)
        ↓
Generate cryptographically secure token (EmailService.GenerateToken)
Store token + expiry (24 hours) in database
        ↓
Send reset email with link: /Account/ResetPassword?userId=X&token=Y
        ↓
User clicks link → validates token + expiry
        ↓
User submits new password → BCrypt hash stored
Token cleared from database
AuditLog "PasswordReset" written
        ↓
Redirect to Login with success message
```

---

## Features

### Authentication and Security
- Custom session-based authentication (no ASP.NET Identity)
- BCrypt password hashing (work factor 11)
- Account lockout after 5 failed attempts (15-minute lockout)
- Email verification gate (must confirm email before login)
- Administrator approval gate (must be approved before login)
- Anti-forgery tokens on all POST forms
- Security headers: X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy
- HTTPS enforcement with HSTS
- Parameterised EF Core queries (SQL injection prevention)
- Razor auto-encoding (XSS prevention)

### KPI Management
- Create, edit, and archive KPIs
- Assign KPIs to departments and BSC perspectives
- Set targets, units, and frequency (Monthly/Quarterly/Annual)
- Log actual values with period and notes
- Automatic status computation (On Track / At Risk / Behind)
- Automatic notifications for off-track KPIs

### Balanced Scorecard
- Four-quadrant view (Financial, Customer, Internal Process, Learning & Growth)
- Each quadrant shows all KPIs with target, actual, and status badge
- Linked to strategic goals

### Strategic Planning
- Create and manage strategic goals
- Link goals to KPIs (many-to-many)
- Track goal status (Not Started / In Progress / Completed / Cancelled)
- Archive/restore goals

### Performance Analytics
- Line chart: average actual value per perspective over 6 months
- Bar chart: % On Track per department
- Doughnut chart: overall On Track / At Risk / Behind split
- Filterable by department and time range (3/6/12 months)

### Executive Reporting
- Full KPI table with variance column
- Scorecard summary by perspective
- Strategic goals list

### User Management (Admin)
- Create, edit, activate/deactivate users
- Assign roles and departments
- Approve/reject pending registrations
- Role hierarchy enforcement (Administrator cannot manage Super Admin)

### Department Management
- Create, edit, archive, and delete departments
- Delete blocked if users or KPIs are assigned

### Notifications
- In-app notification bell with unread count
- Full notification list with read/unread styling
- Automatic creation on KPI status changes

### Audit Log (System Logs)
- Immutable record of every login, logout, create, update, delete
- Visible to Super Admin (all entries) and Administrator (non-Super-Admin entries)
- Captures user, action, entity type, entity ID, details, IP address, timestamp

### Forgot Password
- Secure token-based password reset
- 24-hour token expiry
- Anti-enumeration (same response whether email exists or not)

---

## Test Coverage

The `PeakMetrics.Tests` project contains **37 tests** across 4 test classes:

### AuthenticationTests (5 tests)
| Test | Description | Result |
|---|---|---|
| `Login_WithValidCredentials_ShouldSucceed` | Valid email + password returns success | ✅ Pass |
| `Login_WithInvalidPassword_ShouldFail` | Wrong password returns InvalidCredentials | ✅ Pass |
| `Login_After5FailedAttempts_ShouldLockAccount` | 5 failures triggers lockout | ✅ Pass |
| `Login_WithUnverifiedEmail_ShouldBeRejected` | EmailConfirmed=false blocks login | ✅ Pass |
| `Login_WithUnapprovedAccount_ShouldBeRejected` | IsApproved=false blocks login | ✅ Pass |

### RegistrationTests (5 tests + 12 parameterised)
| Test | Description | Result |
|---|---|---|
| `Register_WithValidData_ShouldCreatePendingAccount` | Valid data creates unapproved account | ✅ Pass |
| `Register_WithBlockedEmailDomain_ShouldFail` (×7) | Blocked domains rejected | ✅ Pass |
| `Register_WithExistingEmail_ShouldFail` | Duplicate email rejected | ✅ Pass |
| `Register_WithWeakPassword_ShouldFail` (×5) | Weak passwords rejected | ✅ Pass |
| `Register_WithMissingName_ShouldFail` | Empty FullName fails validation | ✅ Pass |

### KPITests (5 tests)
| Test | Description | Result |
|---|---|---|
| `KPIStatus_WhenActualMeetsTarget_ShouldBeOnTrack` | actual ≥ target → On Track | ✅ Pass |
| `KPIStatus_WhenActualIs80PercentOfTarget_ShouldBeAtRisk` | 75%–99% → At Risk | ✅ Pass |
| `KPIStatus_WhenActualBelow75Percent_ShouldBeBehind` | <75% → Behind | ✅ Pass |
| `KPILog_WhenStatusIsBehind_ShouldCreateNotification` | Behind entry creates Alert notification | ✅ Pass |
| `CreateKPI_WithMissingName_ShouldFailValidation` | Empty Name fails ModelState | ✅ Pass |

### SecurityTests (7 tests + 9 parameterised)
| Test | Description | Result |
|---|---|---|
| `UnauthenticatedRequest_ToDashboard_ShouldRedirectToLogin` | No session → redirect to Login | ✅ Pass |
| `StaffUser_AccessingAdminPage_ShouldBeForbidden` | Staff cannot access Admin pages | ✅ Pass |
| `StaffUser_AccessingAuditLog_ShouldBeForbidden` | Staff cannot access System Logs | ✅ Pass |
| `Login_WithSQLInjectionInput_ShouldNotCompromiseDatabase` | SQL injection payloads rejected | ✅ Pass |
| `KPIName_WithScriptTag_ShouldBeSanitizedOrRejected` | XSS payloads HTML-encoded | ✅ Pass |
| `PostRequest_WithoutAntiForgeryToken_ShouldReturn400` | [ValidateAntiForgeryToken] present | ✅ Pass |
| `Register_WithPasswordMissingUppercase_ShouldFail` (×5) | Password policy enforced | ✅ Pass |

**Total: 37/37 tests passing**

---

## Running the Tests

```bash
# Run all tests
dotnet test PeakMetrics.Tests/PeakMetrics.Tests.csproj

# Run with detailed output
dotnet test PeakMetrics.Tests/PeakMetrics.Tests.csproj --verbosity normal

# Run a specific test class
dotnet test PeakMetrics.Tests/PeakMetrics.Tests.csproj --filter "ClassName=PeakMetrics.Tests.SecurityTests"
```

Or use **Visual Studio Test Explorer**: Test → Run All Tests

---

## Configuration

### appsettings.json (local development)
```json
{
  "ConnectionStrings": {
    "LocalConnection": "Server=localhost;Database=PeakMetrics;Trusted_Connection=True;"
  },
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": "587",
    "SmtpUser": "your-email@gmail.com",
    "SmtpPass": "your-app-password",
    "FromName": "PeakMetrics",
    "FromEmail": "your-email@gmail.com"
  }
}
```

### Production
Set the `PEAKMETRICS_CONNECTION_STRING` environment variable. Email settings are configured via `appsettings.json` or environment variables.

---

## Seed Accounts

| Email | Password | Role |
|---|---|---|
| admin@peakmetrics.com | Admin@123 | Super Admin |
| manager@peakmetrics.com | Manager@123 | Manager |
| sarah@peakmetrics.com | User@123 | Staff |
| michael@peakmetrics.com | User@123 | Staff |
| emily@peakmetrics.com | User@123 | Staff |
| executive@peakmetrics.com | Executive@123 | Executive |

> These are demo seed accounts only. Change all passwords before deploying to production.

---

## Deployment

```bat
# Build and deploy to IIS
deploy.bat

# Deploy with production secrets
deploy.secrets.bat
```

See `deploy.secrets.example.bat` for the required environment variables.
