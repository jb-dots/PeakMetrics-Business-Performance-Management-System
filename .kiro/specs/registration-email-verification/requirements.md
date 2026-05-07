# Requirements Document

## Introduction

This feature adds a self-service registration system to PeakMetrics. New users can submit a registration request, verify their email address, and then wait for an Administrator or Super Admin to approve their account before they can log in. The flow is: Register → Email Verification → Admin Approval → Login.

The system is built on top of the existing custom session-based authentication (BCrypt + EF Core) and does **not** introduce ASP.NET Core Identity. Email delivery uses MailKit via a new `EmailService`. Seeded accounts are updated to carry the new approval fields so they continue to work without re-seeding.

---

## Glossary

- **Registration_System**: The end-to-end subsystem that handles new-user registration, email verification, and admin approval.
- **Applicant**: A person who has submitted a registration form but whose account has not yet been approved.
- **Email_Validator**: The component that checks an email address for format correctness and blocked domains.
- **Email_Service**: The MailKit-based SMTP service (`EmailService.cs`) responsible for sending all transactional emails.
- **Confirmation_Token**: A time-limited, cryptographically random token stored against the user record and used to verify email ownership.
- **Pending_User**: An `AppUser` whose `EmailConfirmed = true` and `IsApproved = false`.
- **Admin**: Any user with the role `Administrator` or `Super Admin`.
- **Approval_Manager**: The controller action set that allows Admins to approve or reject Pending Users.
- **Audit_Log**: The existing `AuditLog` table used to record significant system events.
- **AppUser**: The existing `PeakMetrics.Web.Models.AppUser` entity, extended with approval fields.
- **AccountController**: The new MVC controller (`AccountController.cs`) that owns all registration and email-confirmation actions.
- **Blocked_Domain**: An email domain that the Registration_System refuses to accept (e.g. `@mailinator.com`).

---

## Requirements

### Requirement 1: Extend the User Model with Approval Fields

**User Story:** As a system architect, I want the `AppUser` entity to carry approval metadata, so that the Registration_System can track whether each account has been verified and approved.

#### Acceptance Criteria

1. THE `AppUser` SHALL include a `bool IsApproved` property that defaults to `false`.
2. THE `AppUser` SHALL include a `DateTime? ApprovedAt` property.
3. THE `AppUser` SHALL include a `string? ApprovedById` property that stores the `Id` of the Admin who approved the account.
4. THE `AppUser` SHALL include a `bool EmailConfirmed` property that defaults to `false`.
5. THE `AppUser` SHALL include a `string? PendingRole` property that defaults to `"Staff"`.
6. THE `AppUser` SHALL include a `string? PendingDepartmentId` property.
7. WHEN the database migration `AddApprovalFields` is applied, THE database schema SHALL reflect all six new columns on the `Users` table.
8. WHEN the application seeds demo accounts, THE `AppUser` seeder SHALL set `IsApproved = true` and `EmailConfirmed = true` for all pre-seeded accounts so that existing login behaviour is preserved.

---

### Requirement 2: Registration Form

**User Story:** As a prospective user, I want to submit a registration form with my details, so that I can request access to PeakMetrics.

#### Acceptance Criteria

1. THE `AccountController` SHALL expose a `GET /Account/Register` action that renders the registration form without requiring an active session.
2. THE registration form SHALL collect: Full Name (required, max 80 characters), Email Address (required, max 100 characters), Password (required, min 8 characters), Confirm Password, Department (dropdown populated from the `Departments` table), and Requested Role (dropdown limited to `Staff` and `Manager`).
3. WHEN the registration form is submitted, THE `AccountController` SHALL validate all fields server-side before processing the request.
4. IF Full Name exceeds 80 characters, THEN THE `AccountController` SHALL return a validation error for that field.
5. IF Email Address exceeds 100 characters, THEN THE `AccountController` SHALL return a validation error for that field.
6. IF Password is fewer than 8 characters, THEN THE `AccountController` SHALL return a validation error for that field.
7. IF Confirm Password does not match Password, THEN THE `AccountController` SHALL return a validation error for that field.
8. THE registration page SHALL use the same split-panel layout as the login page: a blue left panel and a white right panel containing the form.
9. THE blue left panel SHALL display the heading "Join PeakMetrics" and the subtext "Submit your request to join your organization's performance management system."
10. THE registration page SHALL include a "Login instead" link that navigates to `/Home/Login`.

---

### Requirement 3: Email Domain Validation

**User Story:** As a system administrator, I want the Registration_System to reject disposable and internal email addresses, so that only legitimate users can register.

#### Acceptance Criteria

1. WHEN an email address is submitted for registration, THE `Email_Validator` SHALL verify that the address conforms to a valid email format.
2. WHEN an email address is submitted for registration, THE `Email_Validator` SHALL verify that the domain portion contains at least one dot.
3. IF the submitted email domain matches any entry in the Blocked_Domain list (`peakmetrics.com`, `test.com`, `example.com`, `fake.com`, `mailinator.com`, `tempmail.com`, `yopmail.com`), THEN THE `Email_Validator` SHALL reject the address with a descriptive validation error.
4. THE `Email_Validator` SHALL perform domain-blocking checks in a case-insensitive manner.
5. IF the submitted email address is already associated with an existing `AppUser` record, THEN THE `AccountController` SHALL return a validation error stating that the email is already registered.

---

### Requirement 4: Account Creation and Registration Flow

**User Story:** As a prospective user, I want my account to be created in a pending state after I submit the registration form, so that the system can track my request through the verification and approval pipeline.

#### Acceptance Criteria

1. WHEN all registration fields pass validation, THE `AccountController` SHALL create a new `AppUser` record with `IsApproved = false` and `EmailConfirmed = false`.
2. WHEN creating the new `AppUser`, THE `AccountController` SHALL store the selected Department as `DepartmentId` and the selected role as `PendingRole`.
3. WHEN creating the new `AppUser`, THE `AccountController` SHALL hash the password using BCrypt before persisting it.
4. WHEN the new `AppUser` is persisted, THE `AccountController` SHALL generate a `Confirmation_Token` and send a verification email to the Applicant via the `Email_Service`.
5. WHEN the verification email is sent, THE `AccountController` SHALL redirect the Applicant to a confirmation page displaying: "Registration submitted! Please check your email to verify your account. Once verified, an administrator will review and approve your access."
6. THE `AccountController` SHALL NOT create a session or log the Applicant in after registration.

---

### Requirement 5: Email Verification

**User Story:** As an Applicant, I want to click a link in my email to verify my address, so that the system can confirm I own the email I registered with.

#### Acceptance Criteria

1. THE `Email_Service` SHALL send a verification email containing a confirmation link in the format `/Account/ConfirmEmail?userId={id}&token={token}`.
2. THE verification email body SHALL include the text: "Welcome to PeakMetrics! Please verify your email by clicking the button below. After verification, an administrator will review your account before you can access the system."
3. THE `Email_Service` SHALL read SMTP configuration from `appsettings.json` under the key `EmailSettings`, with sub-keys: `SmtpHost`, `SmtpPort`, `SmtpUser`, `SmtpPass`, `FromName`, and `FromEmail`.
4. THE `Email_Service` SHALL be implemented in `Services/EmailService.cs` using the MailKit library.
5. WHEN the Applicant navigates to the confirmation link, THE `AccountController` SHALL locate the `AppUser` by `userId` and validate the `Confirmation_Token`.
6. WHEN the token is valid, THE `AccountController` SHALL set `EmailConfirmed = true` on the `AppUser` and persist the change.
7. WHEN email confirmation succeeds, THE `AccountController` SHALL display: "Email verified successfully! Your account is now pending administrator approval. You will be able to log in once an admin approves your account."
8. WHEN email confirmation succeeds, THE `Email_Service` SHALL send a notification email to every `AppUser` whose `Role` is `Super Admin` or `Administrator`, informing them that a new account is awaiting approval.
9. IF the `userId` does not correspond to an existing `AppUser`, or the `Confirmation_Token` is invalid or expired, THEN THE `AccountController` SHALL display: "Verification link is invalid or has expired. Please register again."

---

### Requirement 6: Login Approval Gate

**User Story:** As a system administrator, I want the login process to block users who have not verified their email or have not been approved, so that only fully vetted users can access the system.

#### Acceptance Criteria

1. WHEN a user submits valid credentials via `POST /Home/Login`, THE `HomeController` SHALL check `EmailConfirmed` before establishing a session.
2. IF `EmailConfirmed` is `false` after a successful password check, THEN THE `HomeController` SHALL sign the user out (clear any partial session), and display the error: "Please verify your email before logging in."
3. WHEN `EmailConfirmed` is `true`, THE `HomeController` SHALL check `IsApproved` before establishing a session.
4. IF `IsApproved` is `false`, THEN THE `HomeController` SHALL sign the user out (clear any partial session), and display the error: "Your account is pending administrator approval."
5. WHEN both `EmailConfirmed` and `IsApproved` are `true`, THE `HomeController` SHALL proceed with the existing session-creation and redirect logic unchanged.

---

### Requirement 7: Pending Users Management Page

**User Story:** As an Admin, I want to see a list of users who have verified their email but are awaiting approval, so that I can approve or reject their access requests.

#### Acceptance Criteria

1. THE `HomeController` SHALL expose a `GET /Home/PendingUsers` action accessible only to users with the role `Administrator` or `Super Admin`.
2. THE Pending Users page SHALL display a table with columns: Full Name, Email, Requested Role, Department, Registration Date, and action buttons for Approve and Reject.
3. THE Pending Users page SHALL list only `AppUser` records where `EmailConfirmed = true` AND `IsApproved = false`.
4. WHEN an Admin clicks Approve for a Pending_User, THE `HomeController` SHALL set `IsApproved = true`, `ApprovedAt = DateTime.UtcNow`, and `ApprovedById` to the current Admin's `Id`.
5. WHEN an Admin approves a Pending_User, THE `HomeController` SHALL assign the user's `Role` from `PendingRole` using the existing role-assignment mechanism, and set `DepartmentId` from `PendingDepartmentId`.
6. WHEN an Admin approves a Pending_User, THE `Email_Service` SHALL send an approval notification email to the newly approved user.
7. WHEN an Admin approves a Pending_User, THE `HomeController` SHALL write an `Audit_Log` entry recording the approval action.
8. WHEN an Admin clicks Reject for a Pending_User, THE `HomeController` SHALL delete the `AppUser` record via the existing delete mechanism.
9. WHEN an Admin rejects a Pending_User, THE `Email_Service` SHALL send a rejection notification email to the rejected user's email address.
10. WHEN an Admin rejects a Pending_User, THE `HomeController` SHALL write an `Audit_Log` entry recording the rejection action.

---

### Requirement 8: Pending Approvals Sidebar Badge

**User Story:** As an Admin, I want to see a badge in the sidebar showing how many users are awaiting approval, so that I can quickly identify when action is needed.

#### Acceptance Criteria

1. THE `_Layout.cshtml` sidebar SHALL include a "Pending Approvals" navigation link visible only to users with the role `Administrator` or `Super Admin`.
2. THE "Pending Approvals" link SHALL be placed under the User Management section in the sidebar.
3. THE "Pending Approvals" link SHALL display a badge showing the count of `AppUser` records where `EmailConfirmed = true` AND `IsApproved = false`.
4. WHEN there are zero Pending Users, THE badge SHALL not be displayed (or display zero).
5. THE badge count SHALL be loaded as part of the existing `OnActionExecutionAsync` filter so it is available on every authenticated page for Admin roles.

---

### Requirement 9: Entry Points — Landing Page and Login Page Links

**User Story:** As a prospective user, I want to find the registration page easily from the landing page and the login page, so that I can start the sign-up process without confusion.

#### Acceptance Criteria

1. THE landing page (`Views/Landing/Index.cshtml`) "Get Started" button SHALL link to `/Account/Register`.
2. THE login page (`Views/Home/Login.cshtml`) SHALL include a "Don't have an account? Register here" link that navigates to `/Account/Register`.
3. THE registration page SHALL include a "Login instead" link that navigates to `/Home/Login`.

---

### Requirement 10: Backward Compatibility

**User Story:** As a system administrator, I want the new registration feature to be additive, so that existing login, role-based access, seeded accounts, and all CRUD functionality continue to work without modification.

#### Acceptance Criteria

1. THE existing `HomeController` login flow SHALL continue to work for all pre-seeded accounts after the `AddApprovalFields` migration is applied.
2. THE `AppUser` seeder in `AppDbContext.cs` SHALL be updated to include `IsApproved = true` and `EmailConfirmed = true` for all six pre-seeded accounts.
3. THE existing role-based access control, session management, and all CRUD actions in `HomeController` SHALL remain unchanged in behaviour.
4. WHEN the `AddApprovalFields` migration has not yet been applied on a host, THE `HomeController` login action SHALL degrade gracefully (the existing try/catch pattern used for lockout columns SHALL be extended to cover the new approval columns).
5. THE `AccountController` SHALL be a separate controller from `HomeController` so that no existing routes are affected.
