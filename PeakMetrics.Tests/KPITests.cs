// ============================================
// PeakMetrics - KPI Module Tests
// IT16 Information Assurance and Security 1
// Student: John Benedic F. Dutaro
// Date: 2026
// Description: Unit tests covering KPI status computation
//              (On Track / At Risk / Behind), automatic
//              notification creation, and KPI creation
//              field validation.
// ============================================

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PeakMetrics.Tests.Helpers;
using PeakMetrics.Web.Models;
using PeakMetrics.Web.ViewModels;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace PeakMetrics.Tests;

/// <summary>
/// Tests the KPI status computation rules and the notification
/// side-effect that fires when a KPI is logged as Behind or At Risk.
/// </summary>
public class KPITests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Simulates saving a KPI log entry: computes status, persists the entry,
    /// and creates a notification if the status is not On Track.
    /// Mirrors the logic in HomeController.KPILogEntry POST.
    /// </summary>
    private static async Task<KpiLogEntry> SimulateLogEntryAsync(
        PeakMetrics.Web.Data.AppDbContext db,
        Kpi kpi,
        decimal actualValue,
        int loggedByUserId,
        CancellationToken ct = default)
    {
        var status = KpiStatusHelper.ComputeStatus(kpi, actualValue);

        var entry = new KpiLogEntry
        {
            KpiId          = kpi.Id,
            LoggedByUserId = loggedByUserId,
            ActualValue    = actualValue,
            Status         = status,
            Period         = DateTime.UtcNow.ToString("MMMM yyyy"),
            LoggedAt       = DateTime.UtcNow
        };

        db.KpiLogEntries.Add(entry);

        // Create notification for non-On-Track statuses
        if (status != KpiStatusHelper.OnTrack)
        {
            var notifType = status == KpiStatusHelper.Behind ? "Alert" : "Warning";
            db.Notifications.Add(new Notification
            {
                UserId    = loggedByUserId,
                KpiId     = kpi.Id,
                Title     = $"KPI {status}: {kpi.Name}",
                Message   = $"Actual value {actualValue} is {status.ToLower()} target {kpi.Target}.",
                Type      = notifType,
                IsRead    = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        return entry;
    }

    private static Kpi MakeKpi(int id, string name, decimal target, string unit = "%")
        => new Kpi
        {
            Id            = id,
            Name          = name,
            Target        = target,
            Unit          = unit,
            Frequency     = "Monthly",
            Status        = "On Track",
            PerspectiveId = 1,
            DepartmentId  = 1
        };

    // ── Test 1 — KPI Status: On Track ─────────────────────────────────────────

    [Fact]
    public void KPIStatus_WhenActualMeetsTarget_ShouldBeOnTrack()
    {
        // Arrange
        var kpi = MakeKpi(1, "Revenue Growth Rate", target: 15m);

        // Act — actual equals target
        var statusEqual = KpiStatusHelper.ComputeStatus(kpi, 15m);
        // Act — actual exceeds target
        var statusAbove = KpiStatusHelper.ComputeStatus(kpi, 20m);

        // Assert
        statusEqual.Should().Be(KpiStatusHelper.OnTrack,
            because: "actual value equal to target must be On Track");
        statusAbove.Should().Be(KpiStatusHelper.OnTrack,
            because: "actual value above target must also be On Track");
    }

    // ── Test 2 — KPI Status: At Risk ──────────────────────────────────────────

    [Fact]
    public void KPIStatus_WhenActualIs80PercentOfTarget_ShouldBeAtRisk()
    {
        // Arrange — target = 100, 80% = 80 (between 75% and 100%)
        var kpi = MakeKpi(2, "Sales Conversion Rate", target: 100m);

        // Act
        var status = KpiStatusHelper.ComputeStatus(kpi, 80m);

        // Assert
        status.Should().Be(KpiStatusHelper.AtRisk,
            because: "actual value at 80% of target (≥75% but <100%) must be At Risk");
    }

    // ── Test 3 — KPI Status: Behind ───────────────────────────────────────────

    [Fact]
    public void KPIStatus_WhenActualBelow75Percent_ShouldBeBehind()
    {
        // Arrange — target = 100, 70% = 70 (below 75%)
        var kpi = MakeKpi(3, "Net Profit Margin", target: 100m);

        // Act
        var status = KpiStatusHelper.ComputeStatus(kpi, 70m);

        // Assert
        status.Should().Be(KpiStatusHelper.Behind,
            because: "actual value below 75% of target must be Behind");
    }

    // ── Test 4 — Notification Created When Behind ─────────────────────────────

    [Fact]
    public async Task KPILog_WhenStatusIsBehind_ShouldCreateNotification()
    {
        // Arrange
        using var db = TestDbContextFactory.Create(seed: ctx =>
        {
            TestDbContextFactory.SeedDepartmentsAndPerspectives(ctx);

            ctx.Users.Add(TestDbContextFactory.CreateApprovedUser(
                id: 10, email: "staff@example.com", plainPassword: "Pass@123"));

            ctx.Kpis.Add(MakeKpi(10, "Net Profit Margin", target: 100m));
            ctx.SaveChanges();
        });

        var kpi = await db.Kpis.FindAsync(10);

        // Act — log a value that is Behind (60% of target)
        var entry = await SimulateLogEntryAsync(db, kpi!, actualValue: 60m, loggedByUserId: 10);

        // Assert — entry status
        entry.Status.Should().Be(KpiStatusHelper.Behind,
            because: "60% of target is below the 75% threshold");

        // Assert — notification was created
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == 10 && n.KpiId == 10);

        notification.Should().NotBeNull(
            because: "a Behind KPI log entry must automatically create a notification");
        notification!.Type.Should().Be("Alert",
            because: "Behind status maps to Alert notification type");
    }

    // ── Test 5 — KPI Creation Validation ──────────────────────────────────────

    [Fact]
    public async Task CreateKPI_WithMissingName_ShouldFailValidation()
    {
        // Arrange
        var model = new KpiFormViewModel
        {
            Name          = "",   // <-- missing
            Unit          = "%",
            Target        = 15m,
            DepartmentId  = 1,
            PerspectiveId = 1
        };

        // Act — validate using DataAnnotations (same as MVC ModelState)
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        // Assert
        isValid.Should().BeFalse(
            because: "KPI Name is a required field and must fail validation when empty");

        validationResults.Should().Contain(r =>
            r.MemberNames.Contains(nameof(KpiFormViewModel.Name)),
            because: "the validation error must be attributed to the Name field");
    }
}
