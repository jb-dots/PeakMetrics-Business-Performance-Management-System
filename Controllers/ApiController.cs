using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeakMetrics.Web.Data;

namespace PeakMetrics.Web.Controllers;

/// <summary>
/// PeakMetrics REST API — returns JSON for all major resources.
///
/// Public endpoints (no login required):
///   GET  /api/health
///   GET  /api/kpis
///   GET  /api/kpis/{id}
///   GET  /api/departments
///   GET  /api/departments/{id}/kpis
///   GET  /api/perspectives
///   GET  /api/strategic-goals
///
/// Auth-required endpoints (active session):
///   GET  /api/kpis/{id}/logs
///   GET  /api/kpis/status-summary
///   GET  /api/kpis/search?q=
///   GET  /api/users
///   GET  /api/users/{id}
///   GET  /api/users/me
///   GET  /api/notifications
///   GET  /api/notifications/unread-count
///   GET  /api/audit-logs
///   GET  /api/scorecard
///   GET  /api/performance-summary
/// </summary>
[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ApiController> _logger;

    public ApiController(AppDbContext db, ILogger<ApiController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // =========================================================================
    // PUBLIC ENDPOINTS — no authentication required
    // =========================================================================

    // ── GET /api/health ───────────────────────────────────────────────────────
    /// <summary>Health check — returns server status and timestamp.</summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status      = "healthy",
            app         = "PeakMetrics",
            version     = "1.0.0",
            timestamp   = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }

    // ── GET /api/kpis ─────────────────────────────────────────────────────────
    /// <summary>
    /// Returns all active KPIs with their latest status and log entry.
    /// Optional query params: ?department=Finance  ?perspective=Financial  ?status=Behind
    /// </summary>
    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis(
        [FromQuery] string? department,
        [FromQuery] string? perspective,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        try
        {
            var query = _db.Kpis
                .Where(k => k.IsActive)
                .Include(k => k.Department)
                .Include(k => k.Perspective)
                .Include(k => k.LogEntries)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(department))
                query = query.Where(k => k.Department.Name == department);

            if (!string.IsNullOrWhiteSpace(perspective))
                query = query.Where(k => k.Perspective.Name == perspective);

            var kpis = await query
                .OrderBy(k => k.Department.Name)
                .ThenBy(k => k.Name)
                .Select(k => new
                {
                    k.Id,
                    k.Name,
                    k.Unit,
                    k.Target,
                    k.Frequency,
                    k.Description,
                    Department  = k.Department.Name,
                    Perspective = k.Perspective.Name,
                    LatestStatus = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => e.Status)
                        .FirstOrDefault() ?? "No Data",
                    LatestValue = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => (decimal?)e.ActualValue)
                        .FirstOrDefault(),
                    LastUpdated = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => (DateTime?)e.LoggedAt)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            // Filter by status in memory (computed field)
            if (!string.IsNullOrWhiteSpace(status))
                kpis = kpis.Where(k => k.LatestStatus.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

            return Ok(new { count = kpis.Count, data = kpis });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/kpis/{id} ────────────────────────────────────────────────────
    /// <summary>Returns a single KPI with full details and latest log entry.</summary>
    [HttpGet("kpis/{id:int}")]
    public async Task<IActionResult> GetKpi(int id, CancellationToken ct)
    {
        try
        {
            var kpi = await _db.Kpis
                .Where(k => k.Id == id && k.IsActive)
                .Include(k => k.Department)
                .Include(k => k.Perspective)
                .Include(k => k.LogEntries)
                .Select(k => new
                {
                    k.Id,
                    k.Name,
                    k.Unit,
                    k.Target,
                    k.Frequency,
                    k.Description,
                    k.CreatedAt,
                    Department  = k.Department.Name,
                    Perspective = k.Perspective.Name,
                    LatestEntry = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => new
                        {
                            e.Id,
                            e.ActualValue,
                            e.Status,
                            e.Period,
                            e.Notes,
                            e.LoggedAt
                        })
                        .FirstOrDefault(),
                    TotalLogEntries = k.LogEntries.Count
                })
                .FirstOrDefaultAsync(ct);

            if (kpi is null)
                return NotFound(new { error = $"KPI with id {id} not found." });

            return Ok(kpi);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/departments ──────────────────────────────────────────────────
    /// <summary>Returns all active departments with user and KPI counts.</summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments(CancellationToken ct)
    {
        try
        {
            var departments = await _db.Departments
                .Where(d => !d.IsArchived)
                .Include(d => d.Users)
                .Include(d => d.Kpis)
                .OrderBy(d => d.Name)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.Description,
                    d.CreatedAt,
                    UserCount = d.Users.Count(u => u.IsActive),
                    KpiCount  = d.Kpis.Count(k => k.IsActive)
                })
                .ToListAsync(ct);

            return Ok(new { count = departments.Count, data = departments });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/departments/{id}/kpis ────────────────────────────────────────
    /// <summary>Returns all active KPIs belonging to a specific department.</summary>
    [HttpGet("departments/{id:int}/kpis")]
    public async Task<IActionResult> GetDepartmentKpis(int id, CancellationToken ct)
    {
        try
        {
            var dept = await _db.Departments.FindAsync(new object[] { id }, ct);
            if (dept is null || dept.IsArchived)
                return NotFound(new { error = $"Department with id {id} not found." });

            var kpis = await _db.Kpis
                .Where(k => k.DepartmentId == id && k.IsActive)
                .Include(k => k.Perspective)
                .Include(k => k.LogEntries)
                .OrderBy(k => k.Name)
                .Select(k => new
                {
                    k.Id,
                    k.Name,
                    k.Unit,
                    k.Target,
                    k.Frequency,
                    Perspective  = k.Perspective.Name,
                    LatestStatus = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => e.Status)
                        .FirstOrDefault() ?? "No Data",
                    LatestValue = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => (decimal?)e.ActualValue)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            return Ok(new
            {
                departmentId   = id,
                departmentName = dept.Name,
                count          = kpis.Count,
                data           = kpis
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/perspectives ─────────────────────────────────────────────────
    /// <summary>Returns all BSC perspectives with their KPI counts.</summary>
    [HttpGet("perspectives")]
    public async Task<IActionResult> GetPerspectives(CancellationToken ct)
    {
        try
        {
            var perspectives = await _db.Perspectives
                .Include(p => p.Kpis)
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    ActiveKpiCount = p.Kpis.Count(k => k.IsActive)
                })
                .ToListAsync(ct);

            return Ok(new { count = perspectives.Count, data = perspectives });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/strategic-goals ──────────────────────────────────────────────
    /// <summary>
    /// Returns all active strategic goals.
    /// Optional query param: ?status=In Progress
    /// </summary>
    [HttpGet("strategic-goals")]
    public async Task<IActionResult> GetStrategicGoals(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        try
        {
            var query = _db.StrategicGoals
                .Where(g => !g.IsArchived)
                .Include(g => g.Perspective)
                .Include(g => g.Owner)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(g => g.Status == status);

            var goals = await query
                .OrderBy(g => g.Status)
                .ThenBy(g => g.Title)
                .Select(g => new
                {
                    g.Id,
                    g.Title,
                    g.Description,
                    g.Status,
                    g.TargetYear,
                    g.CreatedAt,
                    Perspective = g.Perspective.Name,
                    Owner       = g.Owner != null ? g.Owner.FullName : null
                })
                .ToListAsync(ct);

            return Ok(new { count = goals.Count, data = goals });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // =========================================================================
    // AUTH-REQUIRED ENDPOINTS — active session needed
    // =========================================================================

    // ── GET /api/kpis/{id}/logs ───────────────────────────────────────────────
    /// <summary>Returns the full log history for a specific KPI. Requires authentication.</summary>
    [HttpGet("kpis/{id:int}/logs")]
    public async Task<IActionResult> GetKpiLogs(int id, CancellationToken ct)
    {
        if (HttpContext.Session.GetInt32("UserId") is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var kpi = await _db.Kpis.FindAsync(new object[] { id }, ct);
            if (kpi is null)
                return NotFound(new { error = $"KPI with id {id} not found." });

            var logs = await _db.KpiLogEntries
                .Where(e => e.KpiId == id)
                .Include(e => e.LoggedBy)
                .OrderByDescending(e => e.LoggedAt)
                .Select(e => new
                {
                    e.Id,
                    e.ActualValue,
                    e.Status,
                    e.Period,
                    e.Notes,
                    e.LoggedAt,
                    LoggedBy = e.LoggedBy.FullName
                })
                .ToListAsync(ct);

            return Ok(new { kpiId = id, kpiName = kpi.Name, count = logs.Count, data = logs });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/kpis/status-summary ──────────────────────────────────────────
    /// <summary>
    /// Returns a count of KPIs by status (On Track / At Risk / Behind / No Data).
    /// Requires authentication.
    /// </summary>
    [HttpGet("kpis/status-summary")]
    public async Task<IActionResult> GetKpiStatusSummary(CancellationToken ct)
    {
        if (HttpContext.Session.GetInt32("UserId") is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var kpis = await _db.Kpis
                .Where(k => k.IsActive)
                .Include(k => k.LogEntries)
                .Select(k => new
                {
                    LatestStatus = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => e.Status)
                        .FirstOrDefault() ?? "No Data"
                })
                .ToListAsync(ct);

            var summary = new
            {
                total   = kpis.Count,
                onTrack = kpis.Count(k => k.LatestStatus == "On Track"),
                atRisk  = kpis.Count(k => k.LatestStatus == "At Risk"),
                behind  = kpis.Count(k => k.LatestStatus == "Behind"),
                noData  = kpis.Count(k => k.LatestStatus == "No Data")
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/kpis/search?q= ───────────────────────────────────────────────
    /// <summary>
    /// Searches KPIs by name or description. Requires authentication.
    /// Example: /api/kpis/search?q=revenue
    /// </summary>
    [HttpGet("kpis/search")]
    public async Task<IActionResult> SearchKpis([FromQuery] string? q, CancellationToken ct)
    {
        if (HttpContext.Session.GetInt32("UserId") is null)
            return Unauthorized(new { error = "Authentication required." });

        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Query parameter 'q' is required." });

        try
        {
            var results = await _db.Kpis
                .Where(k => k.IsActive &&
                    (k.Name.Contains(q) || (k.Description != null && k.Description.Contains(q))))
                .Include(k => k.Department)
                .Include(k => k.Perspective)
                .OrderBy(k => k.Name)
                .Select(k => new
                {
                    k.Id,
                    k.Name,
                    k.Unit,
                    k.Target,
                    Department  = k.Department.Name,
                    Perspective = k.Perspective.Name
                })
                .ToListAsync(ct);

            return Ok(new { query = q, count = results.Count, data = results });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/users ────────────────────────────────────────────────────────
    /// <summary>Returns all users (basic info only). Requires authentication.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        if (HttpContext.Session.GetInt32("UserId") is null)
            return Unauthorized(new { error = "Authentication required. Please log in first." });

        try
        {
            var users = await _db.Users
                .Include(u => u.Department)
                .OrderBy(u => u.Role)
                .ThenBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Role,
                    Department  = u.Department != null ? u.Department.Name : null,
                    u.IsActive,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .ToListAsync(ct);

            return Ok(new { count = users.Count, data = users });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/users/{id} ───────────────────────────────────────────────────
    /// <summary>Returns a single user by ID. Requires authentication.</summary>
    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> GetUser(int id, CancellationToken ct)
    {
        if (HttpContext.Session.GetInt32("UserId") is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var user = await _db.Users
                .Where(u => u.Id == id)
                .Include(u => u.Department)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Role,
                    Department  = u.Department != null ? u.Department.Name : null,
                    u.IsActive,
                    u.IsApproved,
                    u.EmailConfirmed,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .FirstOrDefaultAsync(ct);

            if (user is null)
                return NotFound(new { error = $"User with id {id} not found." });

            return Ok(user);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/users/me ─────────────────────────────────────────────────────
    /// <summary>Returns the currently logged-in user's profile. Requires authentication.</summary>
    [HttpGet("users/me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var user = await _db.Users
                .Where(u => u.Id == userId)
                .Include(u => u.Department)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    u.Role,
                    Department  = u.Department != null ? u.Department.Name : null,
                    u.IsActive,
                    u.CreatedAt,
                    u.LastLoginAt
                })
                .FirstOrDefaultAsync(ct);

            if (user is null)
                return NotFound(new { error = "Current user not found." });

            return Ok(user);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/notifications ────────────────────────────────────────────────
    /// <summary>
    /// Returns notifications for the currently logged-in user.
    /// Optional query param: ?unreadOnly=true
    /// Requires authentication.
    /// </summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var query = _db.Notifications
                .Where(n => n.UserId == userId)
                .AsQueryable();

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.IsRead,
                    n.CreatedAt,
                    n.KpiId
                })
                .ToListAsync(ct);

            return Ok(new { count = notifications.Count, data = notifications });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/notifications/unread-count ───────────────────────────────────
    /// <summary>Returns the count of unread notifications for the current user. Requires authentication.</summary>
    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> GetUnreadNotificationCount(CancellationToken ct)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var count = await _db.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

            return Ok(new { userId, unreadCount = count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/audit-logs ───────────────────────────────────────────────────
    /// <summary>
    /// Returns the most recent audit log entries (last 100).
    /// Super Admin sees all; other roles see only their own entries.
    /// Requires authentication.
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs(CancellationToken ct)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var role = HttpContext.Session.GetString("UserRole") ?? string.Empty;

            var query = _db.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            // Non-Super-Admin users only see their own audit entries
            if (role != "Super Admin")
                query = query.Where(a => a.UserId == userId);

            var logs = await query
                .OrderByDescending(a => a.OccurredAt)
                .Take(100)
                .Select(a => new
                {
                    a.Id,
                    a.Action,
                    a.EntityType,
                    a.EntityId,
                    a.Details,
                    a.IpAddress,
                    a.OccurredAt,
                    PerformedBy = a.User != null ? a.User.FullName : "System"
                })
                .ToListAsync(ct);

            return Ok(new { count = logs.Count, data = logs });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/scorecard ────────────────────────────────────────────────────
    /// <summary>
    /// Returns the Balanced Scorecard data — all active KPIs grouped by BSC perspective
    /// with their latest actual value and status. Requires authentication.
    /// </summary>
    [HttpGet("scorecard")]
    public async Task<IActionResult> GetScorecard(CancellationToken ct)
    {
        if (HttpContext.Session.GetInt32("UserId") is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var kpis = await _db.Kpis
                .Where(k => k.IsActive)
                .Include(k => k.Perspective)
                .Include(k => k.Department)
                .Include(k => k.LogEntries)
                .OrderBy(k => k.Perspective.Id)
                .ThenBy(k => k.Name)
                .Select(k => new
                {
                    k.Id,
                    k.Name,
                    k.Unit,
                    k.Target,
                    Department  = k.Department.Name,
                    Perspective = k.Perspective.Name,
                    LatestValue = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => (decimal?)e.ActualValue)
                        .FirstOrDefault(),
                    LatestStatus = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => e.Status)
                        .FirstOrDefault() ?? "No Data",
                    LastUpdated = k.LogEntries
                        .OrderByDescending(e => e.LoggedAt)
                        .Select(e => (DateTime?)e.LoggedAt)
                        .FirstOrDefault()
                })
                .ToListAsync(ct);

            // Group by perspective
            var scorecard = kpis
                .GroupBy(k => k.Perspective)
                .Select(g => new
                {
                    perspective = g.Key,
                    kpiCount    = g.Count(),
                    onTrack     = g.Count(k => k.LatestStatus == "On Track"),
                    atRisk      = g.Count(k => k.LatestStatus == "At Risk"),
                    behind      = g.Count(k => k.LatestStatus == "Behind"),
                    kpis        = g.ToList()
                })
                .ToList();

            return Ok(new { perspectives = scorecard.Count, data = scorecard });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/performance-summary ──────────────────────────────────────────
    /// <summary>
    /// Returns a high-level performance summary: total KPIs, status breakdown,
    /// total users, departments, and strategic goals.
    /// Requires authentication.
    /// </summary>
    [HttpGet("performance-summary")]
    public async Task<IActionResult> GetPerformanceSummary(CancellationToken ct)
    {
        if (HttpContext.Session.GetInt32("UserId") is null)
            return Unauthorized(new { error = "Authentication required." });

        try
        {
            var totalKpis        = await _db.Kpis.CountAsync(k => k.IsActive, ct);
            var totalUsers       = await _db.Users.CountAsync(u => u.IsActive, ct);
            var totalDepartments = await _db.Departments.CountAsync(d => !d.IsArchived, ct);
            var totalGoals       = await _db.StrategicGoals.CountAsync(g => !g.IsArchived, ct);

            // Latest status per KPI
            var latestStatuses = await _db.Kpis
                .Where(k => k.IsActive)
                .Include(k => k.LogEntries)
                .Select(k => k.LogEntries
                    .OrderByDescending(e => e.LoggedAt)
                    .Select(e => e.Status)
                    .FirstOrDefault() ?? "No Data")
                .ToListAsync(ct);

            var onTrack = latestStatuses.Count(s => s == "On Track");
            var atRisk  = latestStatuses.Count(s => s == "At Risk");
            var behind  = latestStatuses.Count(s => s == "Behind");
            var noData  = latestStatuses.Count(s => s == "No Data");

            double performancePct = totalKpis > 0
                ? Math.Round((double)onTrack / totalKpis * 100, 1)
                : 0;

            return Ok(new
            {
                generatedAt       = DateTime.UtcNow,
                totalKpis,
                totalUsers,
                totalDepartments,
                totalStrategicGoals = totalGoals,
                kpiStatusBreakdown  = new { onTrack, atRisk, behind, noData },
                overallPerformancePct = performancePct
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/seed ─────────────────────────────────────────────────────────
    /// <summary>
    /// Force-seeds all sample data. Clears existing KPI logs, strategic goals,
    /// notifications, and audit logs then re-inserts everything fresh.
    /// </summary>
    [HttpGet("seed")]
    public async Task<IActionResult> Seed(CancellationToken ct)
    {
        try
        {
            await SampleDataSeeder.ForceSeedAsync(_db, _logger);

            return Ok(new
            {
                success   = true,
                message   = "Sample data seeded successfully.",
                timestamp = DateTime.UtcNow,
                counts    = new
                {
                    kpiLogEntries  = await _db.KpiLogEntries.CountAsync(ct),
                    strategicGoals = await _db.StrategicGoals.CountAsync(ct),
                    goalKpiLinks   = await _db.GoalKpis.CountAsync(ct),
                    notifications  = await _db.Notifications.CountAsync(ct),
                    auditLogs      = await _db.AuditLogs.CountAsync(ct)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seed endpoint failed.");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
