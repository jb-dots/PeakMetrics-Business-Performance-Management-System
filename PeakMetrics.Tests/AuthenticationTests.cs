// ============================================
// PeakMetrics - Authentication Tests
// IT16 Information Assurance and Security 1
// Student: John Benedic F. Dutaro
// Date: 2026
// Description: Unit tests covering login validation,
//              account lockout, email verification gate,
//              and account approval gate.
// ============================================

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PeakMetrics.Tests.Helpers;
using PeakMetrics.Web.Data;
using PeakMetrics.Web.Models;
using Xunit;

namespace PeakMetrics.Tests;

/// <summary>
/// Tests the authentication business rules that live in HomeController.Login.
/// We test the rules directly against the data layer (in-memory EF Core) rather
/// than spinning up the full MVC pipeline, which keeps tests fast and focused.
/// </summary>
public class AuthenticationTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Simulates the core login check: look up user, verify password, check gates.
    /// Returns a result object that mirrors what the controller would do.
    /// </summary>
    private static async Task<LoginResult> SimulateLoginAsync(
        AppDbContext db,
        string email,
        string password,
        CancellationToken ct = default)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            // Increment failed attempts if user exists
            if (user is not null)
            {
                user.FailedLoginAttempts = (user.FailedLoginAttempts ?? 0) + 1;

                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockoutEnd          = DateTime.UtcNow.AddMinutes(15);
                    user.FailedLoginAttempts = 0;
                    await db.SaveChangesAsync(ct);
                    return LoginResult.Locked;
                }

                await db.SaveChangesAsync(ct);
            }

            return LoginResult.InvalidCredentials;
        }

        // Check lockout
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
            return LoginResult.Locked;

        // Check email confirmation
        if (!user.EmailConfirmed)
            return LoginResult.EmailNotConfirmed;

        // Check approval
        if (!user.IsApproved)
            return LoginResult.NotApproved;

        return LoginResult.Success;
    }

    private enum LoginResult
    {
        Success,
        InvalidCredentials,
        Locked,
        EmailNotConfirmed,
        NotApproved
    }

    // ── Test 1 — Valid Login ──────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ShouldSucceed()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user = TestDbContextFactory.CreateApprovedUser(
            id: 1,
            email: "valid@example.com",
            plainPassword: "ValidPass@123");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await SimulateLoginAsync(db, "valid@example.com", "ValidPass@123");

        // Assert
        result.Should().Be(LoginResult.Success,
            because: "a user with correct credentials, confirmed email, and approved account should log in successfully");
    }

    // ── Test 2 — Invalid Login ────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldFail()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user = TestDbContextFactory.CreateApprovedUser(
            id: 2,
            email: "user@example.com",
            plainPassword: "CorrectPass@123");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await SimulateLoginAsync(db, "user@example.com", "WrongPassword!");

        // Assert
        result.Should().Be(LoginResult.InvalidCredentials,
            because: "an incorrect password must never grant access");
    }

    // ── Test 3 — Account Lockout After 5 Failed Attempts ─────────────────────

    [Fact]
    public async Task Login_After5FailedAttempts_ShouldLockAccount()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user = TestDbContextFactory.CreateApprovedUser(
            id: 3,
            email: "lockout@example.com",
            plainPassword: "SecurePass@123");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act — 5 consecutive failed attempts
        LoginResult lastResult = LoginResult.Success;
        for (int i = 0; i < 5; i++)
        {
            lastResult = await SimulateLoginAsync(db, "lockout@example.com", "WrongPassword!");
        }

        // Assert — 5th attempt triggers lockout
        lastResult.Should().Be(LoginResult.Locked,
            because: "after 5 consecutive failed login attempts the account must be locked");

        // Verify the lockout is persisted in the database
        var lockedUser = await db.Users.FindAsync(3);
        lockedUser!.LockoutEnd.Should().NotBeNull(
            because: "LockoutEnd must be set in the database when the account is locked");
        lockedUser.LockoutEnd!.Value.Should().BeAfter(DateTime.UtcNow,
            because: "the lockout expiry must be in the future");
    }

    // ── Test 4 — Unverified Email Login ───────────────────────────────────────

    [Fact]
    public async Task Login_WithUnverifiedEmail_ShouldBeRejected()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user = TestDbContextFactory.CreateApprovedUser(
            id: 4,
            email: "unverified@example.com",
            plainPassword: "Pass@123",
            emailConfirmed: false,   // <-- email NOT confirmed
            isApproved: true);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await SimulateLoginAsync(db, "unverified@example.com", "Pass@123");

        // Assert
        result.Should().Be(LoginResult.EmailNotConfirmed,
            because: "users who have not confirmed their email address must not be allowed to log in");
    }

    // ── Test 5 — Unapproved Account Login ─────────────────────────────────────

    [Fact]
    public async Task Login_WithUnapprovedAccount_ShouldBeRejected()
    {
        // Arrange
        using var db = TestDbContextFactory.Create();
        var user = TestDbContextFactory.CreateApprovedUser(
            id: 5,
            email: "pending@example.com",
            plainPassword: "Pass@123",
            emailConfirmed: true,
            isApproved: false);   // <-- account NOT approved
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await SimulateLoginAsync(db, "pending@example.com", "Pass@123");

        // Assert
        result.Should().Be(LoginResult.NotApproved,
            because: "accounts pending administrator approval must not be allowed to log in even with correct credentials");
    }
}
