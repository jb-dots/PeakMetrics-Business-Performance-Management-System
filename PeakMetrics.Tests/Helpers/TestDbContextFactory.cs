// ============================================
// PeakMetrics - Test Database Context Factory
// IT16 Information Assurance and Security 1
// Student: John Benedic F. Dutaro
// Date: 2026
// Description: Factory for creating in-memory EF Core
//              database contexts for unit testing.
// ============================================

using Microsoft.EntityFrameworkCore;
using PeakMetrics.Web.Data;
using PeakMetrics.Web.Models;

namespace PeakMetrics.Tests.Helpers;

/// <summary>
/// Creates isolated in-memory AppDbContext instances for each test.
/// Each call produces a fresh database with a unique name so tests
/// never share state.
/// </summary>
public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a new in-memory AppDbContext with an optional seed action.
    /// </summary>
    public static AppDbContext Create(string? dbName = null, Action<AppDbContext>? seed = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        // Do NOT call EnsureCreated() — it runs OnModelCreating seed data which
        // pre-populates IDs 1-6 and conflicts with test-specific seed data.
        // The in-memory provider creates tables on first write automatically.

        seed?.Invoke(context);

        return context;
    }

    /// <summary>
    /// Seeds a standard set of departments and perspectives used across tests.
    /// </summary>
    public static void SeedDepartmentsAndPerspectives(AppDbContext db)
    {
        db.Set<Department>().AddRange(
            new Department { Id = 1, Name = "Finance",    Description = "Finance dept" },
            new Department { Id = 2, Name = "HR",         Description = "HR dept" },
            new Department { Id = 3, Name = "Sales",      Description = "Sales dept" },
            new Department { Id = 4, Name = "Operations", Description = "Ops dept" }
        );

        db.Set<Perspective>().AddRange(
            new Perspective { Id = 1, Name = "Financial" },
            new Perspective { Id = 2, Name = "Customer" },
            new Perspective { Id = 3, Name = "Internal Process" },
            new Perspective { Id = 4, Name = "Learning & Growth" }
        );

        db.SaveChanges();
    }

    /// <summary>
    /// Creates a valid approved+confirmed user with a BCrypt-hashed password.
    /// </summary>
    public static AppUser CreateApprovedUser(
        int id,
        string email,
        string plainPassword,
        string role = "Staff",
        bool emailConfirmed = true,
        bool isApproved = true,
        bool isActive = true,
        int? failedAttempts = 0,
        DateTime? lockoutEnd = null)
    {
        return new AppUser
        {
            Id                   = id,
            FullName             = $"Test User {id}",
            Email                = email,
            PasswordHash         = BCrypt.Net.BCrypt.HashPassword(plainPassword),
            Role                 = role,
            IsActive             = isActive,
            IsApproved           = isApproved,
            EmailConfirmed       = emailConfirmed,
            FailedLoginAttempts  = failedAttempts,
            LockoutEnd           = lockoutEnd,
            CreatedAt            = DateTime.UtcNow
        };
    }
}
