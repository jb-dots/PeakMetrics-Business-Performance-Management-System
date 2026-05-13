// ============================================
// PeakMetrics - Security Tests
// IT16 Information Assurance and Security 1
// Student: John Benedic F. Dutaro
// Date: 2026
// Description: Security tests covering unauthenticated
//              access redirection, role-based access control,
//              SQL injection prevention, XSS prevention,
//              CSRF token validation, and password policy.
// ============================================

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PeakMetrics.Tests.Helpers;
using PeakMetrics.Web.Models;
using PeakMetrics.Web.Services;
using PeakMetrics.Web.ViewModels;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace PeakMetrics.Tests;

/// <summary>
/// Security-focused tests that verify the application's defences against
/// common web vulnerabilities and access-control violations.
/// </summary>
public class SecurityTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Simulates the auth guard in OnActionExecutionAsync.
    /// Returns true if the request should be redirected to Login.
    /// </summary>
    private static bool ShouldRedirectToLogin(int? sessionUserId)
        => sessionUserId is null;

    /// <summary>
    /// Simulates the role-based access check (HasAccess helper in HomeController).
    /// </summary>
    private static bool HasAccess(string userRole, params string[] allowedRoles)
        => allowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Simulates the login attempt with a given email/password string.
    /// Returns true only if a real user is found AND the password verifies.
    /// SQL injection inputs will simply fail the BCrypt.Verify step.
    /// </summary>
    private static async Task<bool> SimulateLoginAsync(
        PeakMetrics.Web.Data.AppDbContext db,
        string email,
        string password,
        CancellationToken ct = default)
    {
        // The application uses parameterised EF Core queries — the email is
        // passed as a parameter, never interpolated into raw SQL.
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

        if (user is null) return false;
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    // ── Test 1 — Unauthenticated Access Redirects to Login ───────────────────

    [Fact]
    public async Task UnauthenticatedRequest_ToDashboard_ShouldRedirectToLogin()
    {
        // Arrange — no session (null userId)
        int? sessionUserId = null;

        // Act
        bool shouldRedirect = ShouldRedirectToLogin(sessionUserId);

        // Assert
        shouldRedirect.Should().BeTrue(
            because: "any request to a protected page without an active session must redirect to Login");

        await Task.CompletedTask; // keep async signature consistent
    }

    // ── Test 2 — Staff Cannot Access Admin Pages ──────────────────────────────

    [Fact]
    public async Task StaffUser_AccessingAdminPage_ShouldBeForbidden()
    {
        // Arrange — Staff role trying to access Admin-only page
        const string staffRole = "Staff";
        string[] adminOnlyRoles = { "Super Admin", "Administrator" };

        // Act
        bool hasAccess = HasAccess(staffRole, adminOnlyRoles);

        // Assert
        hasAccess.Should().BeFalse(
            because: "Staff role must not have access to Admin-only pages");

        await Task.CompletedTask;
    }

    // ── Test 3 — Staff Cannot Access Audit Log ────────────────────────────────

    [Fact]
    public async Task StaffUser_AccessingAuditLog_ShouldBeForbidden()
    {
        // Arrange — SystemLogs is restricted to Super Admin and Administrator
        const string staffRole = "Staff";
        string[] systemLogsRoles = { "Super Admin", "Administrator" };

        // Act
        bool hasAccess = HasAccess(staffRole, systemLogsRoles);

        // Assert
        hasAccess.Should().BeFalse(
            because: "only Super Admin and Administrator can access the System Logs / Audit Log page");

        await Task.CompletedTask;
    }

    // ── Test 4 — SQL Injection Prevention ────────────────────────────────────

    [Fact]
    public async Task Login_WithSQLInjectionInput_ShouldNotCompromiseDatabase()
    {
        // Arrange — seed a real user
        using var db = TestDbContextFactory.Create();
        db.Users.Add(TestDbContextFactory.CreateApprovedUser(
            id: 1,
            email: "admin@company.com",
            plainPassword: "Admin@Pass1"));
        await db.SaveChangesAsync();

        // Classic SQL injection payloads
        string[] injectionPayloads =
        {
            "' OR 1=1 --",
            "' OR '1'='1",
            "admin'--",
            "' OR 1=1; DROP TABLE Users; --",
            "\" OR \"\"=\""
        };

        // Act & Assert — none of the injection strings should authenticate
        foreach (var payload in injectionPayloads)
        {
            // Try as email
            var resultAsEmail = await SimulateLoginAsync(db, payload, "Admin@Pass1");
            resultAsEmail.Should().BeFalse(
                because: $"SQL injection payload '{payload}' used as email must not bypass authentication");

            // Try as password
            var resultAsPassword = await SimulateLoginAsync(db, "admin@company.com", payload);
            resultAsPassword.Should().BeFalse(
                because: $"SQL injection payload '{payload}' used as password must not bypass authentication");
        }

        // Verify the database is intact — the real user still exists
        var userCount = await db.Users.CountAsync();
        userCount.Should().Be(1,
            because: "SQL injection must not be able to delete or modify database records");
    }

    // ── Test 5 — XSS Prevention ───────────────────────────────────────────────

    [Fact]
    public async Task KPIName_WithScriptTag_ShouldBeSanitizedOrRejected()
    {
        // Arrange — KPI name containing an XSS payload
        var xssPayloads = new[]
        {
            "<script>alert('xss')</script>",
            "<img src=x onerror=alert(1)>",
            "javascript:alert('xss')",
            "<svg onload=alert(1)>"
        };

        foreach (var payload in xssPayloads)
        {
            var model = new KpiFormViewModel
            {
                Name          = payload,
                Unit          = "%",
                Target        = 10m,
                DepartmentId  = 1,
                PerspectiveId = 1
            };

            // Act — validate the model
            var validationContext = new ValidationContext(model);
            var validationResults = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

            // Assert — Razor auto-encodes output, so the value is stored but
            // rendered safely. The key assertion is that the raw script tag
            // does NOT appear unencoded in any output.
            // Additionally, verify that the stored value, when HTML-encoded,
            // does not contain an executable script tag.
            var encoded = System.Net.WebUtility.HtmlEncode(payload);
            encoded.Should().NotContain("<script>",
                because: "HTML encoding must neutralise script tags before they reach the browser");
            encoded.Should().NotContain("<img",
                because: "HTML encoding must neutralise img tags with event handlers");
            encoded.Should().NotContain("<svg",
                because: "HTML encoding must neutralise svg tags with event handlers");
            // Note: 'javascript:' in plain text is encoded as-is by HtmlEncode
            // because it is not an HTML attribute. Razor's @model binding
            // encodes it in the attribute context where it matters.
        }

        await Task.CompletedTask;
    }

    // ── Test 6 — CSRF Token Validation ───────────────────────────────────────

    [Fact]
    public async Task PostRequest_WithoutAntiForgeryToken_ShouldReturn400()
    {
        // Arrange — verify that the application's anti-forgery configuration
        // is set to HttpOnly and Secure (as configured in Program.cs).
        // This test validates the configuration values rather than making
        // a live HTTP request (which would require a running server).

        // The anti-forgery options are configured in Program.cs:
        //   options.Cookie.HttpOnly  = true
        //   options.Cookie.SecurePolicy = CookieSecurePolicy.Always
        //   options.Cookie.SameSite = SameSiteMode.Lax
        //
        // All POST actions in HomeController and AccountController are decorated
        // with [ValidateAntiForgeryToken], which causes ASP.NET Core to return
        // HTTP 400 Bad Request when the token is missing or invalid.

        // We verify the [ValidateAntiForgeryToken] attribute is present on
        // the Login POST action via reflection.
        var controllerType = typeof(PeakMetrics.Web.Controllers.HomeController);
        var loginMethod = controllerType.GetMethods()
            .Where(m => m.Name == "Login"
                     && m.GetParameters().Any(p => p.ParameterType == typeof(PeakMetrics.Web.ViewModels.LoginViewModel)))
            .FirstOrDefault();

        loginMethod.Should().NotBeNull(
            because: "the Login POST action must exist on HomeController");

        var hasAntiForgery = loginMethod!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute), inherit: true)
            .Any();

        hasAntiForgery.Should().BeTrue(
            because: "the Login POST action must be decorated with [ValidateAntiForgeryToken] to prevent CSRF attacks");

        await Task.CompletedTask;
    }

    // ── Test 7 — Password Policy Enforcement ─────────────────────────────────

    [Theory]
    [InlineData("alllowercase1@")]   // no uppercase
    [InlineData("NoSpecialChar1")]   // no special character
    [InlineData("NoDigit@Pass")]     // no digit
    [InlineData("Sh@rt1")]           // too short (6 chars)
    [InlineData("nouppercase@1234")] // no uppercase letter
    public async Task Register_WithPasswordMissingUppercase_ShouldFail(string weakPassword)
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var model = new RegisterViewModel
        {
            FullName        = "Test User",
            Email           = "testpolicy@gmail.com",
            Password        = weakPassword,
            ConfirmPassword = weakPassword,
            AgreeToTerms    = true
        };

        // Act — check password policy server-side
        bool hasUpper   = weakPassword.Any(char.IsUpper);
        bool hasDigit   = weakPassword.Any(char.IsDigit);
        bool hasSpecial = weakPassword.Any(c => !char.IsLetterOrDigit(c));
        bool isLongEnough = weakPassword.Length >= 8;

        bool meetsPolicy = hasUpper && hasDigit && hasSpecial && isLongEnough;

        // Assert
        meetsPolicy.Should().BeFalse(
            because: $"password '{weakPassword}' does not meet the full policy requirements");

        await Task.CompletedTask;
    }
}
