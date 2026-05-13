// ============================================
// PeakMetrics - Registration Tests
// IT16 Information Assurance and Security 1
// Student: John Benedic F. Dutaro
// Date: 2026
// Description: Unit tests covering user registration
//              including domain blocking, duplicate email
//              detection, password policy, and field validation.
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
/// Tests the registration business rules: domain blocking, duplicate email,
/// password policy enforcement, and required-field validation.
/// </summary>
public class RegistrationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private enum RegistrationResult
    {
        Success,
        BlockedDomain,
        DuplicateEmail,
        ValidationFailed
    }

    /// <summary>
    /// Simulates the registration pipeline: model validation → domain check →
    /// duplicate check → user creation.
    /// </summary>
    private static async Task<RegistrationResult> SimulateRegisterAsync(
        RegisterViewModel model,
        PeakMetrics.Web.Data.AppDbContext db,
        CancellationToken ct = default)
    {
        // 1. Model validation (DataAnnotations)
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        if (!isValid)
            return RegistrationResult.ValidationFailed;

        // 2. Email format check
        if (!EmailValidator.IsValidFormat(model.Email))
            return RegistrationResult.ValidationFailed;

        // 3. Blocked domain check
        if (EmailValidator.IsBlockedDomain(model.Email))
            return RegistrationResult.BlockedDomain;

        // 4. Duplicate email check
        var emailLower = model.Email.Trim().ToLowerInvariant();
        var exists = await db.Users.AnyAsync(u => u.Email.ToLower() == emailLower, ct);
        if (exists)
            return RegistrationResult.DuplicateEmail;

        // 5. Password policy (uppercase + digit + special char)
        if (!MeetsPasswordPolicy(model.Password))
            return RegistrationResult.ValidationFailed;

        // 6. Create user
        var user = new AppUser
        {
            FullName       = model.FullName.Trim(),
            Email          = model.Email.Trim(),
            PasswordHash   = BCrypt.Net.BCrypt.HashPassword(model.Password),
            Role           = "Staff",
            IsApproved     = false,
            EmailConfirmed = false,
            CreatedAt      = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return RegistrationResult.Success;
    }

    /// <summary>
    /// Enforces the password policy: min 8 chars, uppercase, digit, special char.
    /// Mirrors the policy configured in the application.
    /// </summary>
    private static bool MeetsPasswordPolicy(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8) return false;
        bool hasUpper   = password.Any(char.IsUpper);
        bool hasDigit   = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));
        return hasUpper && hasDigit && hasSpecial;
    }

    // ── Test 1 — Valid Registration ───────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_ShouldCreatePendingAccount()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var model = new RegisterViewModel
        {
            FullName        = "Jane Doe",
            Email           = "jane.doe@gmail.com",
            Password        = "Secure@Pass1",
            ConfirmPassword = "Secure@Pass1",
            AgreeToTerms    = true
        };

        // Act
        var result = await SimulateRegisterAsync(model, db);

        // Assert
        result.Should().Be(RegistrationResult.Success,
            because: "valid registration data should create a new account");

        var createdUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "jane.doe@gmail.com");
        createdUser.Should().NotBeNull();
        createdUser!.IsApproved.Should().BeFalse(
            because: "newly registered accounts must be pending administrator approval");
        createdUser.EmailConfirmed.Should().BeFalse(
            because: "newly registered accounts must have unconfirmed email until the link is clicked");
    }

    // ── Test 2 — Blocked Email Domain ─────────────────────────────────────────

    [Theory]
    [InlineData("user@peakmetrics.com")]
    [InlineData("user@test.com")]
    [InlineData("user@mailinator.com")]
    [InlineData("user@example.com")]
    [InlineData("user@fake.com")]
    [InlineData("user@tempmail.com")]
    [InlineData("user@yopmail.com")]
    public async Task Register_WithBlockedEmailDomain_ShouldFail(string blockedEmail)
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var model = new RegisterViewModel
        {
            FullName        = "Test User",
            Email           = blockedEmail,
            Password        = "Secure@Pass1",
            ConfirmPassword = "Secure@Pass1",
            AgreeToTerms    = true
        };

        // Act
        var result = await SimulateRegisterAsync(model, db);

        // Assert
        result.Should().Be(RegistrationResult.BlockedDomain,
            because: $"the domain of '{blockedEmail}' is on the blocked list and must be rejected");
    }

    // ── Test 3 — Duplicate Email ──────────────────────────────────────────────

    [Fact]
    public async Task Register_WithExistingEmail_ShouldFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();

        // Seed an existing user
        db.Users.Add(new AppUser
        {
            Id           = 1,
            FullName     = "Existing User",
            Email        = "existing@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass@1"),
            Role         = "Staff",
            CreatedAt    = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var model = new RegisterViewModel
        {
            FullName        = "New User",
            Email           = "existing@gmail.com",   // same email
            Password        = "NewPass@123",
            ConfirmPassword = "NewPass@123",
            AgreeToTerms    = true
        };

        // Act
        var result = await SimulateRegisterAsync(model, db);

        // Assert
        result.Should().Be(RegistrationResult.DuplicateEmail,
            because: "registering with an already-used email address must be rejected");
    }

    // ── Test 4 — Weak Password ────────────────────────────────────────────────

    [Theory]
    [InlineData("password")]          // no uppercase, no digit, no special
    [InlineData("Password1")]         // no special character
    [InlineData("password@1")]        // no uppercase
    [InlineData("Sh@rt1")]            // too short (6 chars)
    [InlineData("nouppercase@1234")]  // no uppercase letter
    public async Task Register_WithWeakPassword_ShouldFail(string weakPassword)
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var model = new RegisterViewModel
        {
            FullName        = "Test User",
            Email           = "testuser@gmail.com",
            Password        = weakPassword,
            ConfirmPassword = weakPassword,
            AgreeToTerms    = true
        };

        // Act
        var result = await SimulateRegisterAsync(model, db);

        // Assert
        result.Should().Be(RegistrationResult.ValidationFailed,
            because: $"password '{weakPassword}' does not meet the policy (min 8 chars, uppercase, digit, special char)");
    }

    // ── Test 5 — Missing Required Fields ──────────────────────────────────────

    [Fact]
    public async Task Register_WithMissingName_ShouldFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var model = new RegisterViewModel
        {
            FullName        = "",   // <-- missing
            Email           = "noname@gmail.com",
            Password        = "Secure@Pass1",
            ConfirmPassword = "Secure@Pass1",
            AgreeToTerms    = true
        };

        // Act
        var result = await SimulateRegisterAsync(model, db);

        // Assert
        result.Should().Be(RegistrationResult.ValidationFailed,
            because: "FullName is a required field and an empty value must fail model validation");
    }
}
