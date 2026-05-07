# Design Document — Registration & Email Verification

## Overview

This feature adds a self-service registration pipeline to PeakMetrics. The flow is:

```
Register → Email Verification → Admin Approval → Login
```

The implementation is entirely additive: no existing controllers, routes, or models are removed or broken. The feature is built on the existing custom session-based auth stack (BCrypt + EF Core + `HttpContext.Session`) and uses MailKit for transactional email. ASP.NET Core Identity is not introduced.

### Key Design Decisions

- **Separate controller**: All registration and email-confirmation actions live in a new `AccountController`, keeping `HomeController` routes unchanged.
- **No token expiry for MVP**: `ConfirmationToken` is a one-time-use random string with no expiry. The token is cleared (set to `null`) after successful confirmation.
- **Graceful degradation**: The `HomeController.Login` POST wraps the new approval-gate checks in a `try/catch` so that login continues to work on hosts where the `AddApprovalFields` migration has not yet been applied.
- **Seeder update**: All 6 pre-seeded `AppUser` records get `IsApproved = true` and `EmailConfirmed = true` so existing credentials continue to work after migration.
- **Pending Users in HomeController**: The `PendingUsers`, `ApproveUser`, and `RejectUser` actions are added to `HomeController` (not a new controller) to stay consistent with the existing admin-action pattern.

---

## Architecture

```mermaid
flowchart TD
    A[Visitor] -->|GET /Account/Register| AC[AccountController]
    AC -->|POST /Account/Register| AC
    AC -->|Creates AppUser\nIsApproved=false\nEmailConfirmed=false| DB[(SQL Server\nUsers table)]
    AC -->|Sends verification email| ES[EmailService\nMailKit]
    ES -->|SMTP| Mail[Email Provider]

    B[Applicant] -->|GET /Account/ConfirmEmail?userId&token| AC
    AC -->|Sets EmailConfirmed=true| DB
    AC -->|Notifies all Admins| ES

    C[Admin] -->|GET /Home/PendingUsers| HC[HomeController]
    HC -->|Reads pending users| DB
    C -->|POST /Home/ApproveUser/{id}| HC
    HC -->|Sets IsApproved=true\nAssigns Role+DepartmentId| DB
    HC -->|Sends approval email| ES
    HC -->|Writes AuditLog| DB

    C -->|POST /Home/RejectUser/{id}| HC
    HC -->|Deletes AppUser| DB
    HC -->|Sends rejection email| ES
    HC -->|Writes AuditLog| DB

    D[User] -->|POST /Home/Login| HC
    HC -->|Checks EmailConfirmed\nthen IsApproved| DB
    HC -->|Establishes session| Session[HttpContext.Session]
```

### Component Responsibilities

| Component | Responsibility |
|---|---|
| `AccountController` | Register (GET/POST), ConfirmEmail (GET), RegisterConfirmation (GET) |
| `HomeController` | Login gate (modified), PendingUsers, ApproveUser, RejectUser, sidebar badge |
| `EmailService` | Send verification email, send admin notification, send approval/rejection emails |
| `AppUser` (model) | Extended with 6 new approval/verification fields |
| `RegisterViewModel` | Validates registration form input |
| `PendingUserViewModel` | Projects pending user data for the admin table |
| `AddApprovalFields` migration | Adds new columns to the `Users` table |

---

## Components and Interfaces

### AccountController

**File:** `Controllers/AccountController.cs`

```
GET  /Account/Register           → Register()
POST /Account/Register           → Register(RegisterViewModel, CancellationToken)
GET  /Account/ConfirmEmail       → ConfirmEmail(int userId, string token, CancellationToken)
GET  /Account/RegisterConfirmation → RegisterConfirmation()
```

- `Register GET`: Returns the registration view with an empty `RegisterViewModel` and a populated departments list in `ViewBag.Departments`. Redirects to Dashboard if a session already exists.
- `Register POST`: Validates the model, checks for duplicate email, creates `AppUser`, generates `ConfirmationToken`, saves to DB, sends verification email, redirects to `RegisterConfirmation`.
- `ConfirmEmail GET`: Looks up user by `userId`, validates `ConfirmationToken`, sets `EmailConfirmed = true`, clears the token, saves, sends admin notification emails, returns confirmation view.
- `RegisterConfirmation GET`: Returns a static informational view. No session required.

**No `[Authorize]` attribute** on this controller — all actions are public.

### HomeController Additions

**Modified action:** `Login POST` — after password verification, wraps approval-gate checks in `try/catch`:

```csharp
// After password verified and lockout checks pass:
try
{
    var fullUser = await _db.Users.FindAsync(new object[] { userCore.Id }, cancellationToken);
    if (fullUser is not null)
    {
        if (!fullUser.EmailConfirmed)
        {
            HttpContext.Session.Clear();
            ModelState.AddModelError(string.Empty, "Please verify your email before logging in.");
            return View(model);
        }
        if (!fullUser.IsApproved)
        {
            HttpContext.Session.Clear();
            ModelState.AddModelError(string.Empty, "Your account is pending administrator approval.");
            return View(model);
        }
    }
}
catch
{
    // AddApprovalFields migration not yet applied — skip gate, allow login
}
```

**New actions:**

```
GET  /Home/PendingUsers          → PendingUsers(CancellationToken)       [Admin/SuperAdmin]
POST /Home/ApproveUser/{id}      → ApproveUser(int id, CancellationToken) [Admin/SuperAdmin]
POST /Home/RejectUser/{id}       → RejectUser(int id, CancellationToken)  [Admin/SuperAdmin]
```

**Modified filter:** `OnActionExecutionAsync` — for Admin/SuperAdmin roles, queries and sets `ViewData["PendingApprovalsCount"]`.

### EmailService

**File:** `Services/EmailService.cs`

**Interface:** `IEmailService` (registered as scoped in `Program.cs`)

```csharp
public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string toName, int userId, string token, CancellationToken ct = default);
    Task SendAdminNewUserNotificationAsync(string applicantName, string applicantEmail, CancellationToken ct = default);
    Task SendApprovalEmailAsync(string toEmail, string toName, CancellationToken ct = default);
    Task SendRejectionEmailAsync(string toEmail, string toName, CancellationToken ct = default);
}
```

Configuration is read from `appsettings.json` → `EmailSettings` section via `IConfiguration`. MailKit's `SmtpClient` is used directly (not `IEmailSender`).

### EmailValidator (static helper)

**File:** `Services/EmailValidator.cs`

```csharp
public static class EmailValidator
{
    private static readonly HashSet<string> BlockedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "peakmetrics.com", "test.com", "example.com",
        "fake.com", "mailinator.com", "tempmail.com", "yopmail.com"
    };

    public static bool IsValidFormat(string email);
    public static bool IsBlockedDomain(string email);
}
```

Domain blocking is case-insensitive via `StringComparer.OrdinalIgnoreCase`.

---

## Data Models

### AppUser — New Fields

Added to `Models/AppUser.cs`:

```csharp
/// <summary>Set to true by an Admin after reviewing the account.</summary>
public bool IsApproved { get; set; } = false;

/// <summary>UTC timestamp when the account was approved.</summary>
public DateTime? ApprovedAt { get; set; }

/// <summary>Id (as string) of the Admin who approved this account.</summary>
public string? ApprovedById { get; set; }

/// <summary>Set to true after the user clicks the email confirmation link.</summary>
public bool EmailConfirmed { get; set; } = false;

/// <summary>Role requested at registration time. Assigned to Role on approval.</summary>
public string? PendingRole { get; set; } = "Staff";

/// <summary>DepartmentId (as string) requested at registration. Assigned on approval.</summary>
public string? PendingDepartmentId { get; set; }

/// <summary>One-time token for email verification. Cleared after use.</summary>
public string? ConfirmationToken { get; set; }
```

### AppDbContext — Seeder Update

All 6 seeded `AppUser` records in `SeedData()` gain:

```csharp
IsApproved     = true,
EmailConfirmed = true,
```

### Migration: AddApprovalFields

**File:** `Migrations/{timestamp}_AddApprovalFields.cs`

Adds columns to the `Users` table:

| Column | Type | Nullable | Default |
|---|---|---|---|
| `IsApproved` | `bit` | NOT NULL | `0` |
| `ApprovedAt` | `datetime2` | NULL | — |
| `ApprovedById` | `nvarchar(max)` | NULL | — |
| `EmailConfirmed` | `bit` | NOT NULL | `0` |
| `PendingRole` | `nvarchar(max)` | NULL | — |
| `PendingDepartmentId` | `nvarchar(max)` | NULL | — |
| `ConfirmationToken` | `nvarchar(max)` | NULL | — |

The migration also updates the seeder data rows for Ids 1–6 to set `IsApproved = true` and `EmailConfirmed = true`.

### ViewModels

**RegisterViewModel** (`ViewModels/RegisterViewModel.cs`):

```csharp
public sealed class RegisterViewModel
{
    [Required, StringLength(80)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }

    // "Staff" or "Manager"
    public string PendingRole { get; set; } = "Staff";
}
```

**PendingUserViewModel** (`ViewModels/PendingUserViewModel.cs`):

```csharp
public sealed class PendingUserViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? RequestedRole { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime RegistrationDate { get; set; }
}
```

---

## Configuration

### appsettings.json — EmailSettings

```json
{
  "EmailSettings": {
    "SmtpHost": "smtp.example.com",
    "SmtpPort": 587,
    "SmtpUser": "noreply@example.com",
    "SmtpPass": "",
    "FromName": "PeakMetrics",
    "FromEmail": "noreply@example.com"
  }
}
```

`SmtpPass` is left empty in `appsettings.json` and supplied via environment variable or `appsettings.Production.json` (never committed to source control).

### Program.cs — Service Registration

```csharp
// Email
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();
```

---

## View Structure

### Views/Account/Register.cshtml

Mirrors the `Login.cshtml` split-panel layout exactly:

- **Left panel** (blue gradient): heading "Join PeakMetrics", subtext "Submit your request to join your organization's performance management system.", footer copyright.
- **Right panel** (white): logo, form title "Create Account", subtitle "Request access to PeakMetrics.", form fields (Full Name, Email, Password, Confirm Password, Department dropdown, Role dropdown), "Create Account" submit button, "Already have an account? Login instead" link to `/Home/Login`.
- `Layout = null` (standalone page, no sidebar).
- Client-side validation mirrors the Login page pattern (inline JS, no jQuery Validate dependency).

### Views/Account/RegisterConfirmation.cshtml

Standalone page (no layout). Displays:

> "Registration submitted! Please check your email to verify your account. Once verified, an administrator will review and approve your access."

Includes a "Back to Login" link.

### Views/Account/ConfirmEmail.cshtml

Standalone page (no layout). Displays either:

- **Success**: "Email verified successfully! Your account is now pending administrator approval. You will be able to log in once an admin approves your account."
- **Failure**: "Verification link is invalid or has expired. Please register again."

The controller passes a `bool success` via `ViewBag.Success`.

### Views/Home/PendingUsers.cshtml

Uses `_Layout.cshtml`. Displays a table with columns: Full Name, Email, Requested Role, Department, Registration Date, Actions (Approve / Reject buttons). Each action is a `<form>` with `POST` and anti-forgery token. Empty state message when no pending users exist.

### _Layout.cshtml — Sidebar Badge

A new entry is added to the `menuItems` array (or rendered separately after the User Management link) for Admin/SuperAdmin roles:

```razor
@if (sessionUserRole is "Super Admin" or "Administrator")
{
    var pendingCount = (int)(ViewData["PendingApprovalsCount"] ?? 0);
    <a asp-controller="Home" asp-action="PendingUsers"
       class="app-nav-link @(currentAction == "PendingUsers" ? "active" : "")">
        <span class="app-nav-icon"><i class="bi bi-person-check"></i></span>
        <span class="app-nav-label">Pending Approvals</span>
        @if (pendingCount > 0)
        {
            <span class="badge bg-danger ms-auto">@pendingCount</span>
        }
    </a>
}
```

`ViewData["PendingApprovalsCount"]` is populated in `OnActionExecutionAsync` for authenticated Admin/SuperAdmin sessions.

### Views/Home/Login.cshtml — Registration Link

A link is added below the submit button:

```html
<p class="text-center mt-3" style="font-size:0.88rem; color:#64748b;">
    Don't have an account?
    <a href="/Account/Register" style="color:#2563eb; font-weight:600;">Register here</a>
</p>
```

### Views/Landing/Index.cshtml — Get Started Button

The existing "Get Started" button `href` is updated from its current target to `/Account/Register`.

---

## Token Generation

`ConfirmationToken` is generated using `System.Security.Cryptography.RandomNumberGenerator`:

```csharp
private static string GenerateToken()
{
    var bytes = new byte[32];
    RandomNumberGenerator.Fill(bytes);
    return Convert.ToBase64String(bytes)
        .Replace("+", "-").Replace("/", "_").Replace("=", ""); // URL-safe
}
```

The token is stored as plain text on `AppUser.ConfirmationToken`. It is cleared (set to `null`) after successful confirmation to prevent replay.

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

This feature involves business logic (validation, state transitions, access control filtering) that is well-suited to property-based testing. The recommended library is **FsCheck** (for C#/.NET) or **CsCheck**, both of which integrate with xUnit.

---

### Property 1: Seeded accounts are pre-approved and email-confirmed

*For any* pre-seeded `AppUser` (Ids 1–6), `IsApproved` must be `true` and `EmailConfirmed` must be `true`, ensuring existing login behaviour is preserved after the migration.

**Validates: Requirements 1.8, 10.1, 10.2**

---

### Property 2: Registration validation rejects out-of-bound field lengths

*For any* `RegisterViewModel` where `FullName.Length > 80`, or `Email.Length > 100`, or `Password.Length < 8`, or `ConfirmPassword != Password`, server-side model validation must fail and no `AppUser` record must be created.

**Validates: Requirements 2.4, 2.5, 2.6, 2.7**

---

### Property 3: Blocked-domain emails are rejected regardless of case

*For any* email address whose domain (case-insensitively) matches an entry in the blocked-domain list (`peakmetrics.com`, `test.com`, `example.com`, `fake.com`, `mailinator.com`, `tempmail.com`, `yopmail.com`), `EmailValidator.IsBlockedDomain` must return `true`.

**Validates: Requirements 3.3, 3.4**

---

### Property 4: Duplicate email registration is rejected

*For any* email address that already exists in the `Users` table, a `POST /Account/Register` with that email must return a validation error and must not create a second `AppUser` record.

**Validates: Requirements 3.5**

---

### Property 5: Successful registration creates a pending, unauthenticated user

*For any* valid `RegisterViewModel` (all fields pass validation, email is unique and not blocked), the resulting `AppUser` must have `IsApproved = false`, `EmailConfirmed = false`, a non-null `ConfirmationToken`, and no session (`UserId` session key must be absent after the action completes).

**Validates: Requirements 4.1, 4.2, 4.3, 4.6**

---

### Property 6: BCrypt password hashing round-trip

*For any* plaintext password string of length ≥ 8, `BCrypt.Net.BCrypt.Verify(password, BCrypt.Net.BCrypt.HashPassword(password))` must return `true`, and the stored hash must never equal the plaintext password.

**Validates: Requirements 4.3**

---

### Property 7: Email confirmation link format is correct

*For any* `userId` (positive integer) and `token` (non-empty URL-safe string), the verification email body generated by `EmailService` must contain a link matching the pattern `/Account/ConfirmEmail?userId={userId}&token={token}`.

**Validates: Requirements 5.1**

---

### Property 8: Valid token confirms email; invalid token does not

*For any* `AppUser` with a non-null `ConfirmationToken`, navigating to `ConfirmEmail` with the correct `userId` and matching `token` must set `EmailConfirmed = true` and clear `ConfirmationToken`. For any mismatched or absent token, `EmailConfirmed` must remain `false`.

**Validates: Requirements 5.5, 5.6**

---

### Property 9: Login is blocked when EmailConfirmed is false

*For any* `AppUser` with valid credentials (`IsActive = true`, correct password) and `EmailConfirmed = false`, `POST /Home/Login` must not establish a session and must return the error message "Please verify your email before logging in."

**Validates: Requirements 6.1, 6.2**

---

### Property 10: Login is blocked when IsApproved is false

*For any* `AppUser` with valid credentials, `EmailConfirmed = true`, and `IsApproved = false`, `POST /Home/Login` must not establish a session and must return the error message "Your account is pending administrator approval."

**Validates: Requirements 6.3, 6.4**

---

### Property 11: Pending Users query returns exactly the right set

*For any* collection of `AppUser` records with varying `EmailConfirmed` and `IsApproved` values, the query used by `GET /Home/PendingUsers` must return exactly those records where `EmailConfirmed = true AND IsApproved = false` — no more, no fewer.

**Validates: Requirements 7.3**

---

### Property 12: Approval sets all fields correctly and writes an audit log

*For any* pending `AppUser` (with `PendingRole` and `PendingDepartmentId` set) and any Admin user, after `POST /Home/ApproveUser/{id}`: `IsApproved` must be `true`, `ApprovedAt` must be a non-null UTC timestamp, `ApprovedById` must equal the Admin's `Id` (as string), `Role` must equal `PendingRole`, `DepartmentId` must equal the parsed `PendingDepartmentId`, and an `AuditLog` entry with `Action = "ApproveUser"` must exist.

**Validates: Requirements 7.4, 7.5, 7.7**

---

### Property 13: Rejection deletes the user and writes an audit log

*For any* pending `AppUser`, after `POST /Home/RejectUser/{id}`: the `AppUser` record must no longer exist in the database, and an `AuditLog` entry with `Action = "RejectUser"` must exist.

**Validates: Requirements 7.8, 7.10**

---

### Property 14: Pending approvals badge count matches the actual pending user count

*For any* authenticated Admin/SuperAdmin session, `ViewData["PendingApprovalsCount"]` must equal the count of `AppUser` records where `EmailConfirmed = true AND IsApproved = false` at the time the filter runs.

**Validates: Requirements 8.3, 8.5**

---

## Error Handling

| Scenario | Handling |
|---|---|
| SMTP send fails during registration | Log the exception; do not roll back the user record. The user can request a resend (future feature). Surface a generic "We couldn't send the verification email. Please contact support." message. |
| SMTP send fails during approval/rejection | Log the exception; the approval/rejection DB change is already committed. Admin sees a warning toast but the action is not reversed. |
| `ConfirmEmail` called with non-existent `userId` | Return the failure view: "Verification link is invalid or has expired." |
| `ConfirmEmail` called with wrong/null token | Return the failure view: "Verification link is invalid or has expired." |
| `ApproveUser`/`RejectUser` called with non-existent `id` | Return 404. |
| `ApproveUser`/`RejectUser` called by non-Admin | `OnActionExecutionAsync` redirects to `AccessDenied` (existing pattern). |
| `AddApprovalFields` migration not yet applied | `Login POST` wraps approval-gate in `try/catch`; on exception, skips the gate and allows login (existing lockout pattern extended). |
| Duplicate registration attempt | `AccountController` checks for existing email before creating the user; returns a field-level validation error. |

---

## Testing Strategy

### Unit Tests

Focus on pure logic that does not require a running database or SMTP server:

- `EmailValidator.IsValidFormat` — test with valid emails, malformed strings, edge cases (no `@`, no domain, no TLD).
- `EmailValidator.IsBlockedDomain` — test all 7 blocked domains in various cases, plus non-blocked domains.
- `RegisterViewModel` data annotations — test boundary values for `FullName` (80/81 chars), `Email` (100/101 chars), `Password` (7/8 chars), mismatched `ConfirmPassword`.
- Token generation — verify tokens are URL-safe, non-empty, and unique across multiple calls.
- BCrypt round-trip — verify `Verify(password, HashPassword(password))` is always `true`.
- Pending user query predicate — test the LINQ expression `u => u.EmailConfirmed && !u.IsApproved` against in-memory collections.

### Property-Based Tests

Use **FsCheck** (NuGet: `FsCheck.Xunit`) with a minimum of **100 iterations** per property. Each test is tagged with a comment referencing the design property.

```
// Feature: registration-email-verification, Property 2: Registration validation rejects out-of-bound field lengths
// Feature: registration-email-verification, Property 3: Blocked-domain emails are rejected regardless of case
// Feature: registration-email-verification, Property 6: BCrypt password hashing round-trip
// Feature: registration-email-verification, Property 7: Email confirmation link format is correct
// Feature: registration-email-verification, Property 8: Valid token confirms email; invalid token does not
// Feature: registration-email-verification, Property 11: Pending Users query returns exactly the right set
// Feature: registration-email-verification, Property 14: Pending approvals badge count matches actual count
```

Properties 9, 10, 12, 13 involve controller actions with DB side effects; these are tested as integration tests (see below) rather than pure property tests.

### Integration Tests

Use an in-memory SQLite database (via `Microsoft.EntityFrameworkCore.InMemory`) or a test-scoped SQL Server LocalDB instance:

- Full registration → confirm email → approve → login happy path.
- Login blocked for unconfirmed email (Property 9).
- Login blocked for unapproved account (Property 10).
- Approval sets all fields and writes audit log (Property 12).
- Rejection deletes user and writes audit log (Property 13).
- Seeded accounts can log in after migration (Property 1).
- Admin notification emails sent after email confirmation (Requirement 5.8).
- Approval/rejection emails sent (Requirements 7.6, 7.9) — verified via a mock `IEmailService`.

### Smoke / Manual Checks

- `AddApprovalFields` migration applies cleanly on a fresh database and on an existing database.
- `EmailSettings` section is present in `appsettings.json`.
- `AccountController` is a separate file from `HomeController`.
- `Services/EmailService.cs` exists and references MailKit.
- Sidebar badge is visible for Admin/SuperAdmin and hidden for other roles.
- Split-panel layout of Register page matches Login page visually.
- "Get Started" on landing page navigates to `/Account/Register`.
- "Login instead" on Register page navigates to `/Home/Login`.
- "Don't have an account?" link on Login page navigates to `/Account/Register`.
