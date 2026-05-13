using Microsoft.EntityFrameworkCore;
using PeakMetrics.Web.Models;

namespace PeakMetrics.Web.Data;

public static class SampleDataSeeder
{
    /// <summary>
    /// Idempotent seed — skips each section if data already exists.
    /// Called automatically on every app startup.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        await SeedKpiLogEntriesAsync(context, logger);
        await SeedStrategicGoalsAsync(context, logger);
        await SeedGoalKpisAsync(context, logger);
        await SeedNotificationsAsync(context, logger);
        await SeedAuditLogsAsync(context, logger);
    }

    /// <summary>
    /// Force seed — clears existing sample data and re-inserts everything.
    /// Called via GET /api/seed on demand.
    /// </summary>
    public static async Task ForceSeedAsync(AppDbContext context, ILogger logger)
    {
        logger.LogInformation("Force seed: clearing existing sample data...");

        // Remove in FK-safe order
        context.GoalKpis.RemoveRange(context.GoalKpis);
        context.KpiLogEntries.RemoveRange(context.KpiLogEntries);
        context.Notifications.RemoveRange(context.Notifications);
        context.AuditLogs.RemoveRange(context.AuditLogs);
        context.StrategicGoals.RemoveRange(context.StrategicGoals);
        await context.SaveChangesAsync();

        logger.LogInformation("Force seed: existing data cleared. Re-seeding...");

        await SeedKpiLogEntriesAsync(context, logger);
        await SeedStrategicGoalsAsync(context, logger);
        await SeedGoalKpisAsync(context, logger);
        await SeedNotificationsAsync(context, logger);
        await SeedAuditLogsAsync(context, logger);

        logger.LogInformation("Force seed complete.");
    }

    // ── KPI Log Entries ───────────────────────────────────────────────────────

    private static async Task SeedKpiLogEntriesAsync(AppDbContext context, ILogger logger)
    {
        if (await context.KpiLogEntries.AnyAsync())
        {
            logger.LogInformation("KPI log entries already seeded — skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        // (kpiId, name, unit, target, values[0..5] oldest→newest)
        var kpiData = new[]
        {
            (KpiId: 1, Name: "Revenue Growth Rate",      Unit: "%",     Target: 15m,   Values: new[] { 12m, 14m, 16m, 13m, 15m, 17m }),
            (KpiId: 2, Name: "Net Profit Margin",        Unit: "%",     Target: 20m,   Values: new[] { 18m, 19m, 21m, 20m, 22m, 17m }),
            (KpiId: 3, Name: "Employee Turnover Rate",   Unit: "%",     Target: 10m,   Values: new[] {  8m,  9m, 11m, 10m,  7m, 12m }),
            (KpiId: 4, Name: "Training Hours per Staff", Unit: "hrs",   Target: 40m,   Values: new[] { 35m, 38m, 42m, 40m, 36m, 44m }),
            (KpiId: 5, Name: "Sales Conversion Rate",    Unit: "%",     Target: 30m,   Values: new[] { 22m, 25m, 28m, 31m, 27m, 20m }),
            (KpiId: 6, Name: "Customer Satisfaction",    Unit: "score", Target: 4.5m,  Values: new[] { 4.2m, 4.4m, 4.6m, 4.3m, 4.5m, 4.1m }),
            (KpiId: 7, Name: "Process Cycle Time",       Unit: "days",  Target: 3m,    Values: new[] { 2.5m, 3.0m, 3.5m, 2.8m, 4.0m, 2.2m }),
            (KpiId: 8, Name: "Defect Rate",              Unit: "%",     Target: 2m,    Values: new[] { 1.5m, 2.0m, 2.5m, 1.8m, 3.0m, 1.2m }),
        };

        var entries = new List<KpiLogEntry>();

        foreach (var kpi in kpiData)
        {
            for (int i = 0; i < 6; i++)
            {
                // i=0 is 6 months ago, i=5 is 1 month ago
                var monthOffset = 6 - i;
                var logDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddMonths(-monthOffset)
                    .AddDays(14); // 15th of the month

                var actual = kpi.Values[i];
                var status = ComputeStatus(kpi.Name, kpi.Unit, kpi.Target, actual);

                entries.Add(new KpiLogEntry
                {
                    KpiId          = kpi.KpiId,
                    LoggedByUserId = 2,
                    ActualValue    = actual,
                    Status         = status,
                    Notes          = $"Monthly log for {logDate:MMMM yyyy}.",
                    LoggedAt       = logDate,
                    Period         = logDate.ToString("MMMM yyyy"),
                });
            }
        }

        context.KpiLogEntries.AddRange(entries);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} KPI log entries.", entries.Count);
    }

    // ── Strategic Goals ───────────────────────────────────────────────────────

    private static async Task SeedStrategicGoalsAsync(AppDbContext context, ILogger logger)
    {
        if (await context.StrategicGoals.AnyAsync())
        {
            logger.LogInformation("Strategic goals already seeded — skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        var goals = new List<StrategicGoal>
        {
            new StrategicGoal
            {
                Title         = "Achieve 20% Revenue Growth",
                Description   = "Drive revenue growth through expanded sales channels and improved customer retention.",
                PerspectiveId = 1,
                Status        = "In Progress",
                TargetYear    = 2026,
                OwnerUserId   = 2,
                CreatedAt     = now,
                IsArchived    = false,
            },
            new StrategicGoal
            {
                Title         = "Reduce Employee Turnover to 5%",
                Description   = "Improve employee engagement and retention through training programs and competitive compensation.",
                PerspectiveId = 4,
                Status        = "In Progress",
                TargetYear    = 2026,
                OwnerUserId   = 2,
                CreatedAt     = now,
                IsArchived    = false,
            },
            new StrategicGoal
            {
                Title         = "Achieve 4.8 Customer Satisfaction Score",
                Description   = "Enhance customer experience across all touchpoints to achieve industry-leading satisfaction scores.",
                PerspectiveId = 2,
                Status        = "Not Started",
                TargetYear    = 2027,
                OwnerUserId   = 2,
                CreatedAt     = now,
                IsArchived    = false,
            },
            new StrategicGoal
            {
                Title         = "Reduce Process Cycle Time to 2 Days",
                Description   = "Streamline operational processes to reduce cycle time and improve delivery efficiency.",
                PerspectiveId = 3,
                Status        = "In Progress",
                TargetYear    = 2026,
                OwnerUserId   = 2,
                CreatedAt     = now,
                IsArchived    = false,
            },
            new StrategicGoal
            {
                Title         = "Achieve Zero Defect Rate",
                Description   = "Implement quality management systems to drive defect rates toward zero.",
                PerspectiveId = 3,
                Status        = "Not Started",
                TargetYear    = 2027,
                OwnerUserId   = 2,
                CreatedAt     = now,
                IsArchived    = false,
            },
        };

        context.StrategicGoals.AddRange(goals);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} strategic goals.", goals.Count);
    }

    // ── Goal–KPI Links ────────────────────────────────────────────────────────

    private static async Task SeedGoalKpisAsync(AppDbContext context, ILogger logger)
    {
        if (await context.GoalKpis.AnyAsync())
        {
            logger.LogInformation("GoalKpi links already seeded — skipping.");
            return;
        }

        // Resolve goal IDs by title (safe: goals were just inserted above)
        var goalTitles = new[]
        {
            "Achieve 20% Revenue Growth",
            "Reduce Employee Turnover to 5%",
            "Achieve 4.8 Customer Satisfaction Score",
            "Reduce Process Cycle Time to 2 Days",
            "Achieve Zero Defect Rate",
        };

        var goalIds = await context.StrategicGoals
            .Where(g => goalTitles.Contains(g.Title))
            .OrderBy(g => g.Id)
            .Select(g => new { g.Id, g.Title })
            .ToListAsync();

        int GoalId(string title) =>
            goalIds.First(g => g.Title == title).Id;

        var links = new List<GoalKpi>
        {
            // Goal 1 → KPI 1, KPI 2
            new GoalKpi { GoalId = GoalId("Achieve 20% Revenue Growth"),              KpiId = 1 },
            new GoalKpi { GoalId = GoalId("Achieve 20% Revenue Growth"),              KpiId = 2 },
            // Goal 2 → KPI 3, KPI 4
            new GoalKpi { GoalId = GoalId("Reduce Employee Turnover to 5%"),          KpiId = 3 },
            new GoalKpi { GoalId = GoalId("Reduce Employee Turnover to 5%"),          KpiId = 4 },
            // Goal 3 → KPI 5, KPI 6
            new GoalKpi { GoalId = GoalId("Achieve 4.8 Customer Satisfaction Score"), KpiId = 5 },
            new GoalKpi { GoalId = GoalId("Achieve 4.8 Customer Satisfaction Score"), KpiId = 6 },
            // Goal 4 → KPI 7
            new GoalKpi { GoalId = GoalId("Reduce Process Cycle Time to 2 Days"),     KpiId = 7 },
            // Goal 5 → KPI 8
            new GoalKpi { GoalId = GoalId("Achieve Zero Defect Rate"),                KpiId = 8 },
        };

        context.GoalKpis.AddRange(links);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} GoalKpi links.", links.Count);
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    private static async Task SeedNotificationsAsync(AppDbContext context, ILogger logger)
    {
        if (await context.Notifications.AnyAsync())
        {
            logger.LogInformation("Notifications already seeded — skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        var notifications = new List<Notification>
        {
            new Notification
            {
                UserId    = 2,
                Title     = "Revenue Growth Rate — At Risk",
                Message   = "Revenue Growth Rate logged at 12.00% against target 15.00%.",
                Type      = "Warning",
                IsRead    = false,
                CreatedAt = now,
                KpiId     = 1,
            },
            new Notification
            {
                UserId    = 2,
                Title     = "Sales Conversion Rate — Behind",
                Message   = "Sales Conversion Rate logged at 20.00% against target 30.00%.",
                Type      = "Alert",
                IsRead    = false,
                CreatedAt = now,
                KpiId     = 5,
            },
            new Notification
            {
                UserId    = 2,
                Title     = "Employee Turnover Rate — Behind",
                Message   = "Employee Turnover Rate logged at 12.00% against target 10.00%.",
                Type      = "Alert",
                IsRead    = true,
                CreatedAt = now.AddDays(-3),
                KpiId     = 3,
            },
            new Notification
            {
                UserId    = 2,
                Title     = "Customer Satisfaction — At Risk",
                Message   = "Customer Satisfaction logged at 4.10 score against target 4.50.",
                Type      = "Warning",
                IsRead    = true,
                CreatedAt = now.AddDays(-5),
                KpiId     = 6,
            },
            new Notification
            {
                UserId    = 2,
                Title     = "Defect Rate — Behind",
                Message   = "Defect Rate logged at 3.00% against target 2.00%.",
                Type      = "Alert",
                IsRead    = false,
                CreatedAt = now.AddDays(-1),
                KpiId     = 8,
            },
        };

        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} notifications.", notifications.Count);
    }

    // ── Audit Logs ────────────────────────────────────────────────────────────

    private static async Task SeedAuditLogsAsync(AppDbContext context, ILogger logger)
    {
        if (await context.AuditLogs.AnyAsync())
        {
            logger.LogInformation("Audit logs already seeded — skipping.");
            return;
        }

        var now = DateTime.UtcNow;

        var logs = new List<AuditLog>
        {
            new AuditLog
            {
                UserId     = 1,
                Action     = "Login",
                EntityType = "AppUser",
                EntityId   = 1,
                Details    = "Admin logged in successfully.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-28),
            },
            new AuditLog
            {
                UserId     = 2,
                Action     = "Login",
                EntityType = "AppUser",
                EntityId   = 2,
                Details    = "Manager logged in successfully.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-25),
            },
            new AuditLog
            {
                UserId     = 3,
                Action     = "Login",
                EntityType = "AppUser",
                EntityId   = 3,
                Details    = "Staff user Sarah Johnson logged in successfully.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-22),
            },
            new AuditLog
            {
                UserId     = 1,
                Action     = "UserApproved",
                EntityType = "AppUser",
                EntityId   = 3,
                Details    = "Admin approved account for Sarah Johnson (sarah@peakmetrics.com).",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-20),
            },
            new AuditLog
            {
                UserId     = 2,
                Action     = "KpiLogEntry",
                EntityType = "KpiLogEntry",
                EntityId   = 1,
                Details    = "Manager logged Revenue Growth Rate at 12.00% for the period.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-18),
            },
            new AuditLog
            {
                UserId     = 2,
                Action     = "KpiLogEntry",
                EntityType = "KpiLogEntry",
                EntityId   = 2,
                Details    = "Manager logged Net Profit Margin at 18.00% for the period.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-15),
            },
            new AuditLog
            {
                UserId     = 2,
                Action     = "KpiEdit",
                EntityType = "Kpi",
                EntityId   = 5,
                Details    = "Manager updated Sales Conversion Rate target from 25% to 30%.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-12),
            },
            new AuditLog
            {
                UserId     = 2,
                Action     = "Login",
                EntityType = "AppUser",
                EntityId   = 2,
                Details    = "Manager logged in successfully.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-10),
            },
            new AuditLog
            {
                UserId     = 2,
                Action     = "KpiLogEntry",
                EntityType = "KpiLogEntry",
                EntityId   = 3,
                Details    = "Manager logged Employee Turnover Rate at 12.00% for the period.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-5),
            },
            new AuditLog
            {
                UserId     = 1,
                Action     = "Login",
                EntityType = "AppUser",
                EntityId   = 1,
                Details    = "Admin logged in successfully.",
                IpAddress  = "127.0.0.1",
                OccurredAt = now.AddDays(-2),
            },
        };

        context.AuditLogs.AddRange(logs);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} audit log entries.", logs.Count);
    }

    // ── Status computation ────────────────────────────────────────────────────

    private static string ComputeStatus(string kpiName, string unit, decimal target, decimal actual)
    {
        bool lowerIsBetter =
            string.Equals(unit, "days", StringComparison.OrdinalIgnoreCase)
            || kpiName.Contains("Turnover",  StringComparison.OrdinalIgnoreCase)
            || kpiName.Contains("Defect",    StringComparison.OrdinalIgnoreCase)
            || kpiName.Contains("Cycle",     StringComparison.OrdinalIgnoreCase);

        if (lowerIsBetter)
        {
            if (actual <= target)             return "On Track";
            if (actual <= target * 1.25m)     return "At Risk";
            return "Behind";
        }
        else
        {
            if (actual >= target)             return "On Track";
            if (actual >= target * 0.75m)     return "At Risk";
            return "Behind";
        }
    }
}
