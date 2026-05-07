# Implementation Plan: Registration & Email Verification

## Overview

Implement the self-service registration pipeline for PeakMetrics following the flow: Register → Email Verification → Admin Approval → Login. The implementation is entirely additive — no existing routes, controllers, or models are removed. All tasks are in C# / ASP.NET Core MVC using the existing EF Core + BCrypt + session-based auth stack.

## Tasks

- [x] 1. Extend AppUser model, create migration, and update seeder
  - Add 7 new properties to `Models/AppUser.cs`: `IsApproved`, `ApprovedAt`, `ApprovedById`, `EmailConfirmed`, `PendingRole`, `PendingDepartmentId`, `ConfirmationToken` — with the defaults and XML doc comments specified in the design
  - Scaffold a new EF Core migration named `AddApprovalFields` that adds the 7 columns to the `Users` table with the nullability and defaults from the design
  - Update all 6 seeded `AppUser` records in `AppDbContext.SeedData()` to include `IsApproved = true` and `EmailConfirmed = true`
  - Verify the migration applies cleanly with `dotnet ef database update`
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 10.2_

  - [-] 1.1 Write property test for seeded accounts (Property 1)
    - **Property 1: Seeded accounts are pre-approved and email-confirmed**
    - For each of the 6 seeded `AppUser` records (Ids 1–6), assert `IsApproved == true` and `EmailConfirmed == true`
    - Use FsCheck.Xunit; tag with `// Feature: registration-email-verification, Property 1`
    - **Validates: Requirements 1.8, 10.1, 10.2**

- [x] 2. Implement EmailService with MailKit
  - Add `MailKit` NuGet package to `PeakMetrics.Web.csproj`
  - Create `Services/EmailSettings.cs` — a POCO bound to the `EmailSettings` config section
  - Add the `EmailSettings` block to `appsettings.json` (with empty `SmtpPass`)
  - Create `Services/EmailValidator.cs` with `IsValidFormat` and `IsBlockedDomain` static methods; blocked domains: `peakmetrics.com`, `test.com`, `example.com`, `fake.com`, `mailinator.com`, `tempmail.com`, `yopmail.com`; domain comparison is case-insensitive
  - Create `Services/IEmailService.cs` with the 4 method signatures from the design
  - Create `Services/EmailService.cs` implementing `IEmailService` using MailKit's `SmtpClient`; include the private `GenerateToken()` helper using `RandomNumberGenerator.Fill`
  - Register `EmailSettings` options and `IEmailService` / `EmailService` as scoped in `Program.cs`
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 3.1, 3.2, 3.3, 3.4_

  - [ ]* 2.1 Write unit tests for EmailValidator
    - Test `IsValidFormat` with valid emails, no `@`, no domain, no TLD, empty string
    - Test `IsBlockedDomain` for all 7 blocked domains in mixed case, plus non-blocked domains
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [ ]* 2.2 Write property test for blocked-domain rejection (Property 3)
    - **Property 3: Blocked-domain emails are rejected regardless of case**
    - Generate arbitrary casing variants of each blocked domain and assert `IsBlockedDomain` returns `true`
    - Use FsCheck.Xunit; tag with `// Feature: registration-email-verification, Property 3`
    - **Validates: Requirements 3.3, 3.4**

  - [ ]* 2.3 Write property test for email confirmation link format (Property 7)
    - **Property 7: Email confirmation link format is correct**
    - For arbitrary positive `userId` and non-empty URL-safe `token`, assert the generated email body contains `/Account/ConfirmEmail?userId={userId}&token={token}`
    - Use FsCheck.Xunit; tag with `// Feature: registration-email-verification, Property 7`
    - **Validates: Requirements 5.1**

- [x] 3. Implement AccountController — Register GET/POST and ConfirmEmail
  - Create `ViewModels/RegisterViewModel.cs` with the fields and data annotations from the design (`FullName`, `Email`, `Password`, `ConfirmPassword`, `DepartmentId`, `PendingRole`)
  - Create `ViewModels/PendingUserViewModel.cs` with the 6 fields from the design
  - Create `Controllers/AccountController.cs` with no `[Authorize]` attribute; inject `AppDbContext`, `IEmailService`, and `IConfiguration`
  - Implement `GET /Account/Register`: redirect to Dashboard if session exists; populate `ViewBag.Departments`; return view with empty `RegisterViewModel`
  - Implement `POST /Account/Register`: validate model state; run `EmailValidator.IsValidFormat` and `IsBlockedDomain`; check for duplicate email; hash password with BCrypt; create `AppUser` with `IsApproved = false`, `EmailConfirmed = false`, `PendingRole`, `PendingDepartmentId`; generate `ConfirmationToken` via `GenerateToken()`; save to DB; call `SendVerificationEmailAsync`; redirect to `RegisterConfirmation`; do NOT create a session
  - Implement `GET /Account/ConfirmEmail`: look up user by `userId`; validate `ConfirmationToken`; set `EmailConfirmed = true`, clear token; save; call `SendAdminNewUserNotificationAsync` to all Admin/SuperAdmin users; set `ViewBag.Success`; return view
  - Implement `GET /Account/RegisterConfirmation`: return static view (no session required)
  - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 5.5, 5.6, 5.7, 5.8, 5.9, 10.5_

  - [ ]* 3.1 Write property test for registration validation (Property 2)
    - **Property 2: Registration validation rejects out-of-bound field lengths**
    - Generate `RegisterViewModel` instances with `FullName.Length > 80`, `Email.Length > 100`, `Password.Length < 8`, or `ConfirmPassword != Password`; assert model validation fails and no `AppUser` is created
    - Use FsCheck.Xunit; tag with `// Feature: registration-email-verification, Property 2`
    - **Validates: Requirements 2.4, 2.5, 2.6, 2.7**

  - [ ]* 3.2 Write property test for duplicate email rejection (Property 4)
    - **Property 4: Duplicate email registration is rejected**
    - For any email already present in the DB, assert `POST /Account/Register` returns a validation error and does not create a second `AppUser`
    - Use FsCheck.Xunit; tag with `// Feature: registration-email-verification, Property 4`
    - **Validates: Requirements 3.5**

  - [ ]* 3.3 Write property test for successful registration state (Property 5)
    - **Property 5: Successful registration creates a pending, unauthenticated user**
    - For any valid `RegisterViewModel`, assert the resulting `AppUser` has `IsApproved = false`, `EmailConfirmed = false`, non-null `ConfirmationToken`, and no session key `UserId`
    - Use FsCheck.Xunit; tag with `// Feature: registration-email-verification, Property 5`
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.6**

  - [ ]* 3.4 Write property test for BCrypt round-trip (Property 6)
    - **Property 6: BCrypt password hashing round-trip**
    - For any password string of length ≥ 8, assert `BCrypt.Verify(password, BCrypt.HashPassword(password)) == true` and the hash never equals the plaintext
    - Use FsCheck.Xunit; tag with `// Feature: registration-email-verification, Property 6`
    - **Validates: Requirements 4.3**

  - [ ]* 3.5 Write property test for token confirmation (Property 8)
    - **Property 8: Valid token confirms email; invalid token does not**
    - For an `AppUser` with a non-null `ConfirmationToken`, assert correct `userId` + matching token sets `EmailConfirmed = true` and clears the token; assert mismatched or absent token leaves `EmailConfirmed = false`
    - Use FsCheck.Xunit; tag with `// Feature: registration-email-verification, Property 8`
    - **Validates: Requirements 5.5, 5.6**

- [ ] 4. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update HomeController Login — approval gate
  - In `HomeController.Login POST`, after the existing lockout check passes and before the session is established, add a `try/catch` block that loads the full `AppUser` via `FindAsync` and checks `EmailConfirmed` then `IsApproved`
  - If `EmailConfirmed == false`: clear session, add model error "Please verify your email before logging in.", return view
  - If `IsApproved == false`: clear session, add model error "Your account is pending administrator approval.", return view
  - Wrap the entire gate in `try/catch` so login degrades gracefully when the `AddApprovalFields` migration has not yet been applied (matching the existing lockout try/catch pattern)
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 10.1, 10.4_

  - [ ]* 5.1 Write integration test for login blocked — unconfirmed email (Property 9)
    - **Property 9: Login is blocked when EmailConfirmed is false**
    - For any `AppUser` with valid credentials and `EmailConfirmed = false`, assert `POST /Home/Login` does not establish a session and returns the error "Please verify your email before logging in."
    - Use in-memory EF Core provider; tag with `// Feature: registration-email-verification, Property 9`
    - **Validates: Requirements 6.1, 6.2**

  - [ ]* 5.2 Write integration test for login blocked — unapproved account (Property 10)
    - **Property 10: Login is blocked when IsApproved is false**
    - For any `AppUser` with valid credentials, `EmailConfirmed = true`, and `IsApproved = false`, assert `POST /Home/Login` does not establish a session and returns the error "Your account is pending administrator approval."
    - Use in-memory EF Core provider; tag with `// Feature: registration-email-verification, Property 10`
    - **Validates: Requirements 6.3, 6.4**

- [x] 6. Implement PendingUsers page and approval/rejection actions in HomeController
  - Add `GET /Home/PendingUsers` action: require Admin/SuperAdmin (via existing `OnActionExecutionAsync` role check); query `AppUser` records where `EmailConfirmed = true && !IsApproved`; include `Department`; project to `PendingUserViewModel`; return view
  - Add `POST /Home/ApproveUser/{id}` action: load user; set `IsApproved = true`, `ApprovedAt = DateTime.UtcNow`, `ApprovedById = currentAdminId.ToString()`, `Role = PendingRole`, `DepartmentId = int.Parse(PendingDepartmentId)`; save; call `SendApprovalEmailAsync`; write `AuditLog` with `Action = "ApproveUser"`; redirect to `PendingUsers`
  - Add `POST /Home/RejectUser/{id}` action: load user; capture email and name; delete `AppUser`; save; call `SendRejectionEmailAsync`; write `AuditLog` with `Action = "RejectUser"`; redirect to `PendingUsers`; return 404 if user not found
  - Create `Views/Home/PendingUsers.cshtml` using `_Layout.cshtml`; table with columns Full Name, Email, Requested Role, Department, Registration Date, Actions (Approve / Reject); each action is a `<form>` POST with anti-forgery token; empty-state message when no pending users
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7, 7.8, 7.9, 7.10_

  - [ ]* 6.1 Write property test for pending users query (Property 11)
    - **Property 11: Pending Users query returns exactly the right set**
    - For any collection of `AppUser` records with varying `EmailConfirmed` and `IsApproved` values, assert the query returns exactly those where `EmailConfirmed = true AND IsApproved = false`
    - Use FsCheck.Xunit with in-memory collections; tag with `// Feature: registration-email-verification, Property 11`
    - **Validates: Requirements 7.3**

  - [ ]* 6.2 Write integration test for approval — field correctness and audit log (Property 12)
    - **Property 12: Approval sets all fields correctly and writes an audit log**
    - For any pending `AppUser` with `PendingRole` and `PendingDepartmentId` set, after `POST /Home/ApproveUser/{id}`: assert `IsApproved = true`, `ApprovedAt` is non-null UTC, `ApprovedById` equals the Admin's Id as string, `Role = PendingRole`, `DepartmentId = parsed PendingDepartmentId`, and an `AuditLog` entry with `Action = "ApproveUser"` exists
    - Use in-memory EF Core provider; tag with `// Feature: registration-email-verification, Property 12`
    - **Validates: Requirements 7.4, 7.5, 7.7**

  - [ ]* 6.3 Write integration test for rejection — user deleted and audit log (Property 13)
    - **Property 13: Rejection deletes the user and writes an audit log**
    - For any pending `AppUser`, after `POST /Home/RejectUser/{id}`: assert the `AppUser` record no longer exists and an `AuditLog` entry with `Action = "RejectUser"` exists
    - Use in-memory EF Core provider; tag with `// Feature: registration-email-verification, Property 13`
    - **Validates: Requirements 7.8, 7.10**

- [x] 7. Add Pending Approvals sidebar badge
  - In `HomeController.OnActionExecutionAsync`, for authenticated Admin/SuperAdmin sessions, query `_db.Users.CountAsync(u => u.EmailConfirmed && !u.IsApproved)` and set `ViewData["PendingApprovalsCount"]`; wrap in `try/catch` for graceful degradation before migration
  - In `Views/Shared/_Layout.cshtml`, after the User Management nav link, add the "Pending Approvals" nav entry with `bi-person-check` icon, visible only to `Super Admin` and `Administrator` roles; render the danger badge when `ViewData["PendingApprovalsCount"] > 0`
  - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ]* 7.1 Write property test for badge count accuracy (Property 14)
    - **Property 14: Pending approvals badge count matches the actual pending user count**
    - For any collection of `AppUser` records, assert `ViewData["PendingApprovalsCount"]` equals the count of records where `EmailConfirmed = true AND IsApproved = false`
    - Use FsCheck.Xunit with in-memory EF Core; tag with `// Feature: registration-email-verification, Property 14`
    - **Validates: Requirements 8.3, 8.5**

- [ ] 8. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Create Register page view (split layout matching Login)
  - Create `Views/Account/Register.cshtml` with `Layout = null` (standalone, no sidebar)
  - Left panel (blue gradient): heading "Join PeakMetrics", subtext "Submit your request to join your organization's performance management system.", footer copyright — matching the Login page's left-panel structure and CSS classes
  - Right panel (white): logo, form title "Create Account", subtitle "Request access to PeakMetrics.", form fields (Full Name, Email, Password, Confirm Password, Department dropdown from `ViewBag.Departments`, Role dropdown limited to Staff/Manager), "Create Account" submit button, "Already have an account? Login instead" link to `/Home/Login`
  - Include anti-forgery token; add inline client-side validation mirroring the Login page pattern (no jQuery Validate dependency)
  - Create `Views/Account/RegisterConfirmation.cshtml` (standalone, no layout): success message, "Back to Login" link
  - Create `Views/Account/ConfirmEmail.cshtml` (standalone, no layout): success message when `ViewBag.Success == true`, failure message otherwise
  - _Requirements: 2.1, 2.2, 2.8, 2.9, 2.10, 4.5, 5.7, 5.9_

- [x] 10. Update landing page and login page cross-links
  - In `Views/Landing/Index.cshtml`, update the "Get Started" button `href` from `/Home/Login` to `/Account/Register`
  - In `Views/Home/Login.cshtml`, add a "Don't have an account? Register here" link below the submit button pointing to `/Account/Register`
  - _Requirements: 9.1, 9.2, 9.3_

- [ ] 11. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP
- Property tests use **FsCheck.Xunit** (NuGet: `FsCheck.Xunit`) with a minimum of 100 iterations per property
- Integration tests use the `Microsoft.EntityFrameworkCore.InMemory` provider
- Each property test must include a comment referencing the design property number and the feature name
- The approval gate in `HomeController.Login` and the badge count query in `OnActionExecutionAsync` must both be wrapped in `try/catch` for graceful degradation before the `AddApprovalFields` migration is applied
- `AccountController` must remain a separate file from `HomeController` — no existing routes are affected
- `SmtpPass` must never be committed to source control; supply it via environment variable or `appsettings.Production.json`
