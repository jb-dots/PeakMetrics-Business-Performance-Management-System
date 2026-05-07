using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeakMetrics.Web.Data;

namespace PeakMetrics.Web.Controllers;

/// <summary>
/// PeakMetrics REST API — returns JSON for all major resources.
/// Public endpoints: GET /api/kpis, /api/departments, /api/strategic-goals
/// Auth-required:    GET /api/users, /api/kpis/{id}/logs
/// </summary>
[Route("api")]
[ApiController]
public class ApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public ApiController(AppDbContext db) => _db = db;

    // ── GET /api/kpis ─────────────────────────────────────────────────────────
    /// <summary>Returns all active KPIs with their latest status and log entry.</summary>
    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis(CancellationToken ct)
    {
        try
        {
            var kpis = await _db.Kpis
                .Where(k => k.IsActive)
                .Include(k => k.Department)
                .Include(k => k.Perspective)
                .Include(k => k.LogEntries)
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

            return Ok(new { count = kpis.Count, data = kpis });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/kpis/{id} ────────────────────────────────────────────────────
    /// <summary>Returns a single KPI with its full details and latest log entry.</summary>
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

    // ── GET /api/kpis/{id}/logs ───────────────────────────────────────────────
    /// <summary>Returns the log history for a specific KPI. Requires authentication.</summary>
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

    // ── GET /api/strategic-goals ──────────────────────────────────────────────
    /// <summary>Returns all active strategic goals.</summary>
    [HttpGet("strategic-goals")]
    public async Task<IActionResult> GetStrategicGoals(CancellationToken ct)
    {
        try
        {
            var goals = await _db.StrategicGoals
                .Where(g => !g.IsArchived)
                .Include(g => g.Perspective)
                .Include(g => g.Owner)
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

    // ── GET /api/health ───────────────────────────────────────────────────────
    /// <summary>Health check endpoint — returns server status and timestamp.</summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status    = "healthy",
            app       = "PeakMetrics",
            version   = "1.0.0",
            timestamp = DateTime.UtcNow,
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }
}
