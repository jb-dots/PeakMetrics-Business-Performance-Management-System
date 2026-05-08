# Implementation Plan: Forgot Password

## Overview

This implementation adds a secure password reset capability to PeakMetrics using ASP.NET Core Identity's token-based password reset system. The feature integrates with the existing custom session-based authentication, uses MailKit for email delivery, and implements anti-enumeration protection to prevent email discovery attacks. All password reset events are logged to the AuditLog table for security monitoring.

## Tasks

- [x] 1. Install required NuGet packages
  - Add Microsoft.AspNetCore.Identity.EntityFrameworkCore package (version 8.0.*)
  - _Requirements: Design dependencies section_

- [x] 2. Configure ASP.NET Core Identity in Program.cs
  - Add Identity services with password requirements matching existing BCrypt validation
  - Configure token lifespan to 24 hours using DataProtectionTokenProviderOptions
  - Add AddEntityFrameworkStores<AppDbContext>() and AddDefaultTokenProviders()
  - _Requirements: 3.4, 7.2_

- [x] 3. Create ViewModels for password reset forms
  - [x] 3.1 Create ForgotPasswordViewModel
    - Add Email property with Required, EmailAddress, and StringLength(100) validation attributes
    - _Requirements: 2.1, 2.3, 2.4_
  
  - [x] 3.2 Create ResetPasswordViewModel
    - Add UserId, Token, NewPassword, and ConfirmPassword properties
    - Add Required, StringLength, and Compare validation attributes
    - _Requirements: 5.2, 6.1, 6.2_

- [x] 4. Extend IEmailService and EmailService with password reset email method
  - [x] 4.1 Add SendPasswordResetEmailAsync method signature to IEmailService interface
    - Method parameters: toEmail, toName, userId, token, baseUrl, CancellationToken
    - _Requirements: 4.1, 4.2_
  
  - [x] 4.2 Implement SendPasswordResetEmailAsync in EmailService
    - URL-encode the token using WebUtility.UrlEncode
    - Generate reset link with format: /Account/ResetPassword?userId={userId}&token={encodedToken}
    - Create HTML email body with reset button and plain text link
    - Include 24-hour expiry notice and security message
    - _Requirements: 4.2, 4.3_
  
  - [ ]* 4.3 Write property test for email link format
    - **Property 3: Password reset email link format**
    - **Validates: Requirements 4.3**
    - Generate random userIds (positive integers) and tokens (strings with special characters)
    - Verify email body contains correctly URL-encoded link matching pattern
    - Use FsCheck or CsCheck with minimum 100 iterations

- [x] 5. Implement ForgotPassword actions in AccountController
  - [x] 5.1 Add UserManager<AppUser> dependency injection to AccountController constructor
    - Update constructor to accept UserManager<AppUser> parameter
    - _Requirements: 3.2_
  
  - [x] 5.2 Implement ForgotPassword GET action
    - Return view with empty ForgotPasswordViewModel
    - No [Authorize] attribute (public access)
    - _Requirements: 2.1_
  
  - [x] 5.3 Implement ForgotPassword POST action
    - Validate ModelState for email format
    - Query database for user by email (case-insensitive)
    - If user exists: generate token via GeneratePasswordResetTokenAsync, send email
    - If user does not exist: add 150ms artificial delay for anti-enumeration
    - Always display identical success message regardless of email existence
    - Handle email sending failures gracefully (log but don't expose to user)
    - _Requirements: 2.3, 2.4, 3.1, 3.2, 3.3, 4.1, 4.4, 4.5, 9.1, 9.2, 9.3, 10.1_
  
  - [ ]* 5.4 Write property test for invalid email format validation
    - **Property 1: Invalid email format validation**
    - **Validates: Requirements 2.4**
    - Generate random invalid email strings (no @, multiple @, no domain, no TLD)
    - Verify all rejected with correct error message and no database query
    - Use FsCheck or CsCheck with minimum 100 iterations
  
  - [ ]* 5.5 Write property test for anti-enumeration protection
    - **Property 2: Anti-enumeration protection**
    - **Validates: Requirements 3.3, 9.1, 9.2**
    - Generate random email addresses (some existing in test DB, some not)
    - Verify identical success message displayed for all
    - Verify email sent only for existing addresses
    - Verify no information leakage through response behavior
    - Use FsCheck or CsCheck with minimum 100 iterations
  
  - [ ]* 5.6 Write property test for email failure handling
    - **Property 4: Email failure does not expose information**
    - **Validates: Requirements 4.5**
    - Mock IEmailService to throw exceptions (SMTP errors, network timeouts)
    - Verify standard success message still displayed
    - Verify error logged internally but not exposed to user
    - Use FsCheck or CsCheck with minimum 100 iterations

- [x] 6. Implement ResetPassword actions in AccountController
  - [x] 6.1 Implement ResetPassword GET action
    - Accept userId and token query parameters
    - If parameters missing/invalid: display error message with link to ForgotPassword
    - If parameters present: return view with ResetPasswordViewModel containing userId and token
    - No [Authorize] attribute (public access)
    - _Requirements: 5.1, 5.4_
  
  - [x] 6.2 Implement ResetPassword POST action
    - Validate ModelState for password requirements and confirmation match
    - Query database for user by userId
    - Call UserManager.ResetPasswordAsync with user, token, and new password
    - If successful: create AuditLog record, redirect to Login with success toast via TempData
    - If token invalid/expired: display error message with link to request new reset
    - If password doesn't meet requirements: display Identity error messages
    - _Requirements: 6.1, 6.2, 6.3, 7.1, 7.2, 7.3, 7.4, 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 10.2, 10.3, 10.4_
  
  - [ ]* 6.3 Write property test for password confirmation mismatch validation
    - **Property 5: Password confirmation mismatch validation**
    - **Validates: Requirements 6.2**
    - Generate random pairs of non-matching password strings
    - Verify all rejected with correct error message
    - Verify UserManager.ResetPasswordAsync not called
    - Use FsCheck or CsCheck with minimum 100 iterations
  
  - [ ]* 6.4 Write property test for password update on valid token
    - **Property 6: Password update on valid token**
    - **Validates: Requirements 7.2**
    - Generate random valid passwords (6+ characters, various character sets)
    - Create valid reset tokens for test users
    - Verify password updated in database after reset
    - Verify user can login with new password
    - Use FsCheck or CsCheck with minimum 100 iterations
  
  - [ ]* 6.5 Write property test for comprehensive audit logging
    - **Property 7: Comprehensive audit logging**
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.4, 8.5, 8.6**
    - Generate random password reset operations
    - Verify each creates AuditLog record with Action="PasswordReset", EntityType="Auth"
    - Verify Details contains user email in correct format
    - Verify UserId and OccurredAt fields correctly populated (within 5 seconds)
    - Use FsCheck or CsCheck with minimum 100 iterations

- [x] 7. Create ForgotPassword view
  - Create Views/Account/ForgotPassword.cshtml with split-panel layout
  - Left panel: blue gradient with heading "Forgot Your Password?", subtext, footer
  - Right panel: logo, form title "Reset Password", email input field, submit button
  - Add "Remember your password? Login instead" link to /Home/Login
  - Display success message when ViewBag.Success is true
  - Add client-side validation matching Login page pattern
  - Set Layout = null (standalone page)
  - _Requirements: 2.1, 2.2, 10.1_

- [x] 8. Create ResetPassword view
  - Create Views/Account/ResetPassword.cshtml with split-panel layout
  - Left panel: blue gradient with heading "Reset Your Password", subtext, footer
  - Right panel: logo, form title "Create New Password", password fields, submit button
  - Add hidden fields for UserId and Token
  - Display error message when ViewBag.Error is true with link to ForgotPassword
  - Add client-side validation matching Login page pattern
  - Set Layout = null (standalone page)
  - _Requirements: 5.2, 5.3, 10.4_

- [x] 9. Add Forgot Password link to Login page
  - Open Views/Home/Login.cshtml
  - Add "Forgot Password?" link below password field and above submit button
  - Style link to match existing design (right-aligned, small font, link color)
  - Link href: /Account/ForgotPassword
  - _Requirements: 1.1, 1.2_

- [x] 10. Checkpoint - Ensure all tests pass
  - Run all property-based tests and verify they pass
  - Run all unit tests and verify they pass
  - Manually test forgot password flow end-to-end
  - Verify email delivery works in development environment
  - Check audit logs are created correctly
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 11. Write integration tests for complete password reset flow
  - [ ]* 11.1 Test happy path: forgot password → receive email → reset password → login
    - Use in-memory SQLite or test-scoped SQL Server LocalDB
    - Configure ASP.NET Core Identity for test environment
    - Mock IEmailService to capture sent emails
    - Verify complete flow works end-to-end
  
  - [ ]* 11.2 Test token expiry behavior
    - Generate token and mock time to simulate expiry
    - Verify reset fails with correct error message
  
  - [ ]* 11.3 Test invalid token handling
    - Attempt reset with random/malformed tokens
    - Verify error message displayed correctly
  
  - [ ]* 11.4 Test UserManager integration
    - Verify GeneratePasswordResetTokenAsync called with correct parameters
    - Verify ResetPasswordAsync called with correct parameters
  
  - [ ]* 11.5 Test audit logging integration
    - Verify AuditLog records created in database with correct values
    - Query database after successful reset to confirm record exists

- [x] 12. Final checkpoint - Verify deployment readiness
  - Verify all required NuGet packages installed
  - Verify Program.cs Identity configuration is correct
  - Verify all views render correctly
  - Verify email template renders correctly with clickable link
  - Verify success toast appears on Login page after reset
  - Verify error messages display correctly for invalid/expired tokens
  - Test in staging environment with real SMTP server
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Property tests validate universal correctness properties from the design document
- Integration tests validate end-to-end flows and component interactions
- The design uses C# (ASP.NET Core), so all implementation will be in C#
- Identity is configured but NOT used for authentication (existing session-based auth remains unchanged)
- Identity is ONLY used for password reset token generation and validation
- Anti-enumeration protection is critical: identical messages and similar response times for existing/non-existing emails
- All password reset events must be logged to AuditLog for security monitoring
- Email sending failures must not block the password reset request or expose information to users
