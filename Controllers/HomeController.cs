using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using PeakMetrics.Web.Data;
using PeakMetrics.Web.Models;
using PeakMetrics.Web.ViewModels;

namespace PeakMetrics.Web.Controllers;

public class HomeController : Controller
{
    private const string SessionUserId       = "UserId";
    private const string SessionUserName     = "UserName";
    private const string SessionUserRole     = "UserRole";
    private const string SessionUserInitials = "UserInitials";

    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    // ── Auth guard ────────────────────────────────────────────────────────────
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var action = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        var isPublic = string.Equals(action, nameof(Login),  StringComparison.OrdinalIgnoreCase)
                    || string.Equals(action, nameof(Logout), StringComparison.OrdinalIgnoreCase);

        if (!isPublic && HttpContext.Session.GetInt32(SessionUserId) is null)
        {
            context.Result = RedirectToAction(nameof(Login));
            return;
        }

        if (!isPublic)
        {
            ViewData["SessionUserName"]     = HttpContext.Session.GetString(SessionUserName);
            ViewData["SessionUserRole"]     = HttpContext.Session.GetString(SessionUserRole);
            ViewData["SessionUserInitials"] = HttpContext.Session.GetString(SessionUserInitials);

            var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 0;
            var isNotificationsPage = string.Equals(action, nameof(Notifications), StringComparison.OrdinalIgnoreCase);

            if (!isNotificationsPage)
            {
                var quickNotifs = await _db.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(5)
                    .Select(n => new NotificationItemViewModel
                    {
                        Title    = n.Title,
                        Message  = n.Message,
                        Time     = ToRelativeTime(n.CreatedAt),
                        Read     = n.IsRead,
                        Icon     = n.Icon,
                        Severity = n.Severity == "Critical" ? AlertSeverity.Critical
                                 : n.Severity == "Warning"  ? AlertSeverity.Warning
                                 : AlertSeverity.Standard
                    })
                    .ToListAsync(context.HttpContext.RequestAborted);

                ViewData["QuickNotifications"]       = quickNotifs;
                ViewData["UnreadQuickNotifications"] = quickNotifs.Count(n => !n.Read);
            }
            else
            {
                ViewData["QuickNotifications"]       = new List<NotificationItemViewModel>();
                ViewData["UnreadQuickNotifications"] = 0;
            }
        }

        await next();
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    public IActionResult Index() => RedirectToAction(nameof(Login));

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetInt32(SessionUserId) is not null)
            return RedirectToAction(nameof(Dashboard));

        ViewData["Title"] = "Login";
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Login";

        if (!ModelState.IsValid)
            return View(model);

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == model.Email && u.IsActive, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var parts    = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = parts.Length >= 2
            ? $"{parts[0][0]}{parts[^1][0]}"
            : user.FullName[..Math.Min(2, user.FullName.Length)];

        HttpContext.Session.SetInt32(SessionUserId,        user.Id);
        HttpContext.Session.SetString(SessionUserName,     user.FullName);
        HttpContext.Session.SetString(SessionUserRole,     user.Role);
        HttpContext.Session.SetString(SessionUserInitials, initials.ToUpperInvariant());

        await _db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, DateTime.UtcNow), cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = user.Id,
            Action     = "Login",
            EntityType = "AppUser",
            EntityId   = user.Id,
            Details    = $"{user.FullName} signed in.",
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        return RedirectToAction(nameof(Dashboard));
    }

    // ── Logout ────────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId   = HttpContext.Session.GetInt32(SessionUserId);
        var userName = HttpContext.Session.GetString(SessionUserName) ?? "Unknown";

        HttpContext.Session.Clear();

        if (userId.HasValue)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId     = userId.Value,
                Action     = "Logout",
                EntityType = "AppUser",
                EntityId   = userId.Value,
                Details    = $"{userName} signed out.",
                OccurredAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Login));
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Dashboard";

        var role = HttpContext.Session.GetString(SessionUserRole) ?? string.Empty;
        return role switch
        {
            "Admin"         => await BuildSuperAdminDashboardAsync(cancellationToken),
            "Administrator" => await BuildAdminDashboardAsync(cancellationToken),
            "Manager"       => await BuildManagerDashboardAsync(cancellationToken),
            "User"          => await BuildStaffDashboardAsync(cancellationToken),
            "Executive"     => await BuildExecutiveDashboardAsync(cancellationToken),
            _               => RedirectToAction(nameof(Login))
        };
    }

    // ── Super Admin Dashboard Builder ─────────────────────────────────────────
    private async Task<IActionResult> BuildSuperAdminDashboardAsync(CancellationToken ct)
    {
        var totalUsers        = await _db.Users.CountAsync(ct);
        var totalDepartments  = await _db.Departments.CountAsync(ct);
        var totalKpis         = await _db.Kpis.CountAsync(k => k.IsActive, ct);
        var totalAuditEntries = await _db.AuditLogs.CountAsync(ct);
        var totalNotifications = await _db.Notifications.CountAsync(ct);
        var totalKpiLogEntries = await _db.KpiLogEntries.CountAsync(ct);

        var recentAuditLogs = await _db.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.OccurredAt)
            .Take(10)
            .Select(a => new AuditLogRowViewModel(
                a.User != null ? a.User.FullName : "System",
                a.Action,
                a.EntityType,
                ToRelativeTime(a.OccurredAt)))
            .ToListAsync(ct);

        var roleGroups = await _db.Users
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var roleDistribution = roleGroups.ToDictionary(g => g.Role, g => g.Count);

        var departments = await _db.Departments
            .Select(d => new DepartmentOverviewViewModel(
                d.Name,
                d.Users.Count,
                d.Kpis.Count(k => k.IsActive)))
            .ToListAsync(ct);

        return View("Dashboard", new SuperAdminDashboardViewModel
        {
            TotalUsers         = totalUsers,
            TotalDepartments   = totalDepartments,
            TotalKpis          = totalKpis,
            TotalAuditEntries  = totalAuditEntries,
            TotalNotifications = totalNotifications,
            TotalKpiLogEntries = totalKpiLogEntries,
            RecentAuditLogs    = recentAuditLogs,
            RoleDistribution   = roleDistribution,
            Departments        = departments
        });
    }

    // ── Administrator Dashboard Builder ───────────────────────────────────────
    private async Task<IActionResult> BuildAdminDashboardAsync(CancellationToken ct)
    {
        var totalUsers       = await _db.Users.CountAsync(ct);
        var totalDepartments = await _db.Departments.CountAsync(ct);

        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var loggedKpiIdsThisMonth = await _db.KpiLogEntries
            .Where(e => e.LoggedAt >= currentMonth)
            .Select(e => e.KpiId)
            .Distinct()
            .ToListAsync(ct);

        var pendingKpis = await _db.Kpis
            .CountAsync(k => k.IsActive && !loggedKpiIdsThisMonth.Contains(k.Id), ct);

        var newUsersThisMonth = await _db.Users
            .CountAsync(u => u.CreatedAt >= currentMonth, ct);

        var unreadNotifications = await _db.Notifications
            .CountAsync(n => !n.IsRead, ct);

        var recentUsers = await _db.Users
            .Include(u => u.Department)
            .OrderByDescending(u => u.CreatedAt)
            .Take(10)
            .Select(u => new UserRowViewModel(
                u.FullName,
                u.Role,
                u.Department != null ? u.Department.Name : "—",
                u.CreatedAt.ToString("MMM d, yyyy")))
            .ToListAsync(ct);

        var departments = await _db.Departments
            .Select(d => new DepartmentOverviewViewModel(
                d.Name,
                d.Users.Count,
                d.Kpis.Count(k => k.IsActive)))
            .ToListAsync(ct);

        return View("Dashboard", new AdministratorDashboardViewModel
        {
            TotalUsers          = totalUsers,
            TotalDepartments    = totalDepartments,
            PendingKpis         = pendingKpis,
            NewUsersThisMonth   = newUsersThisMonth,
            UnreadNotifications = unreadNotifications,
            RecentUsers         = recentUsers,
            Departments         = departments
        });
    }

    // ── Manager Dashboard Builder ─────────────────────────────────────────────
    private async Task<IActionResult> BuildManagerDashboardAsync(CancellationToken ct)
    {
        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 0;

        // Latest entry per KPI
        var latestByKpi = await _db.KpiLogEntries
            .GroupBy(e => e.KpiId)
            .Select(g => g.OrderByDescending(e => e.LoggedAt).First())
            .ToListAsync(ct);

        var totalKpis = await _db.Kpis.CountAsync(k => k.IsActive, ct);
        var onTrack   = latestByKpi.Count(e => e.Status == "On Track");
        var atRisk    = latestByKpi.Count(e => e.Status == "At Risk");
        var behind    = latestByKpi.Count(e => e.Status == "Behind");

        // Dept KPI statuses — need department names, so load KPIs with departments
        var activeKpisWithDept = await _db.Kpis
            .Where(k => k.IsActive)
            .Include(k => k.Department)
            .Include(k => k.LogEntries)
            .ToListAsync(ct);

        var latestByKpiId = latestByKpi.ToDictionary(e => e.KpiId);

        var deptKpiStatuses = activeKpisWithDept
            .GroupBy(k => k.Department.Name)
            .Select(g =>
            {
                var deptOnTrack = g.Count(k => latestByKpiId.TryGetValue(k.Id, out var e) && e.Status == "On Track");
                var deptAtRisk  = g.Count(k => latestByKpiId.TryGetValue(k.Id, out var e) && e.Status == "At Risk");
                var deptBehind  = g.Count(k => latestByKpiId.TryGetValue(k.Id, out var e) && e.Status == "Behind");
                return new DeptKpiStatusViewModel(g.Key, deptOnTrack, deptAtRisk, deptBehind);
            })
            .ToList();

        // Trend data: last 6 calendar months
        var now         = DateTime.UtcNow;
        var sixMonthsAgo = now.AddMonths(-5);
        var cutoff      = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Build 6 month label list
        var monthSlots = Enumerable.Range(0, 6)
            .Select(i => cutoff.AddMonths(i))
            .ToList();
        var trendLabels = monthSlots.Select(m => m.ToString("MMM yyyy")).ToList();

        var trendEntries = await _db.KpiLogEntries
            .Where(e => e.LoggedAt >= cutoff)
            .Include(e => e.Kpi).ThenInclude(k => k.Department)
            .ToListAsync(ct);

        // Group by (DepartmentName, Year, Month) → average ActualValue
        var trendGrouped = trendEntries
            .GroupBy(e => (DeptName: e.Kpi.Department.Name, Year: e.LoggedAt.Year, Month: e.LoggedAt.Month))
            .ToDictionary(g => g.Key, g => g.Average(e => e.ActualValue));

        var deptNames = trendEntries.Select(e => e.Kpi.Department.Name).Distinct().OrderBy(n => n).ToList();

        var trendDatasets = deptNames.Select(dept =>
        {
            var values = monthSlots.Select(slot =>
            {
                var key = (DeptName: dept, Year: slot.Year, Month: slot.Month);
                return trendGrouped.TryGetValue(key, out var avg) ? (decimal?)avg : null;
            }).ToList();
            return new TrendDatasetViewModel(dept, values);
        }).ToList();

        // Active strategic goals
        var activeGoals = await _db.StrategicGoals
            .Where(g => g.Status != "Cancelled" && g.Status != "Completed")
            .Select(g => new StrategicGoalRowViewModel(
                g.Title,
                g.Perspective,
                g.Status,
                g.DueDate.HasValue ? g.DueDate.Value.ToString("MMM d, yyyy") : null))
            .ToListAsync(ct);

        // Recent KPI logs (top 5)
        var recentLogs = await _db.KpiLogEntries
            .Include(e => e.Kpi)
            .Include(e => e.LoggedBy)
            .OrderByDescending(e => e.LoggedAt)
            .Take(5)
            .Select(e => new RecentKpiLogViewModel(
                e.Kpi.Name,
                e.LoggedBy.FullName,
                FormatValue(e.ActualValue, e.Kpi.Unit),
                e.Status,
                ToRelativeTime(e.LoggedAt)))
            .ToListAsync(ct);

        var unreadNotifications = await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return View("Dashboard", new ManagerDashboardViewModel
        {
            TotalKpis           = totalKpis,
            OnTrack             = onTrack,
            AtRisk              = atRisk,
            Behind              = behind,
            UnreadNotifications = unreadNotifications,
            DeptKpiStatuses     = deptKpiStatuses,
            TrendLabels         = trendLabels,
            TrendDatasets       = trendDatasets,
            ActiveGoals         = activeGoals,
            RecentLogs          = recentLogs
        });
    }

    // ── Staff Dashboard Builder ───────────────────────────────────────────────
    private async Task<IActionResult> BuildStaffDashboardAsync(CancellationToken ct)
    {
        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 0;

        // Get departmentId from DB using session userId
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DepartmentId })
            .FirstOrDefaultAsync(ct);

        var departmentId = user?.DepartmentId;

        if (departmentId is null)
        {
            var unreadCount = await _db.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

            return View("Dashboard", new StaffDashboardViewModel
            {
                HasDepartment       = false,
                UnreadNotifications = unreadCount
            });
        }

        var userDeptId = departmentId.Value;
        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Active KPIs in user's department with latest log entry
        var deptKpis = await _db.Kpis
            .Where(k => k.IsActive && k.DepartmentId == userDeptId)
            .Include(k => k.LogEntries)
            .ToListAsync(ct);

        var myKpiList = deptKpis.Select(k =>
        {
            var latest = k.LogEntries.OrderByDescending(e => e.LoggedAt).FirstOrDefault();
            var status = latest?.Status ?? "No Data";
            return new KpiRowViewModel(
                k.Name,
                k.Perspective,
                FormatValue(k.Target, k.Unit),
                latest != null ? FormatValue(latest.ActualValue, k.Unit) : "—",
                status);
        }).ToList();

        var myKpis  = myKpiList.Count;
        var onTrack = myKpiList.Count(k => k.Status == "On Track");
        var atRisk  = myKpiList.Count(k => k.Status == "At Risk");
        var behind  = myKpiList.Count(k => k.Status == "Behind");

        // Pending KPIs: no entry in current month
        var loggedKpiIdsThisMonth = await _db.KpiLogEntries
            .Where(e => e.LoggedAt >= currentMonth && deptKpis.Select(k => k.Id).Contains(e.KpiId))
            .Select(e => e.KpiId)
            .Distinct()
            .ToListAsync(ct);

        var pendingKpiNames = deptKpis
            .Where(k => !loggedKpiIdsThisMonth.Contains(k.Id))
            .Select(k => k.Name)
            .ToList();

        // My recent logs (top 5 by this user)
        var myRecentLogs = await _db.KpiLogEntries
            .Where(e => e.LoggedByUserId == userId)
            .Include(e => e.Kpi)
            .OrderByDescending(e => e.LoggedAt)
            .Take(5)
            .Select(e => new RecentKpiLogViewModel(
                e.Kpi.Name,
                string.Empty,
                FormatValue(e.ActualValue, e.Kpi.Unit),
                e.Status,
                ToRelativeTime(e.LoggedAt)))
            .ToListAsync(ct);

        // Scorecard: group by perspective
        var scorecard = myKpiList
            .GroupBy(k => k.Perspective)
            .Select(g => new ScorecardPerspectiveViewModel(
                g.Key,
                g.Count(k => k.Status == "On Track"),
                g.Count(k => k.Status == "At Risk"),
                g.Count(k => k.Status == "Behind")))
            .ToList();

        var unreadNotifications = await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return View("Dashboard", new StaffDashboardViewModel
        {
            MyKpis              = myKpis,
            OnTrack             = onTrack,
            AtRisk              = atRisk,
            Behind              = behind,
            UnreadNotifications = unreadNotifications,
            HasDepartment       = true,
            MyKpiList           = myKpiList,
            PendingKpiNames     = pendingKpiNames,
            MyRecentLogs        = myRecentLogs,
            Scorecard           = scorecard
        });
    }

    // ── Executive Dashboard Builder ───────────────────────────────────────────
    private async Task<IActionResult> BuildExecutiveDashboardAsync(CancellationToken ct)
    {
        var totalKpis = await _db.Kpis.CountAsync(k => k.IsActive, ct);

        // Latest entry per KPI
        var latestByKpi = await _db.KpiLogEntries
            .GroupBy(e => e.KpiId)
            .Select(g => g.OrderByDescending(e => e.LoggedAt).First())
            .ToListAsync(ct);

        var kpisWithEntries = latestByKpi.Count;
        var onTrackCount    = latestByKpi.Count(e => e.Status == "On Track");
        var atRiskCount     = latestByKpi.Count(e => e.Status == "At Risk");
        var behindCount     = latestByKpi.Count(e => e.Status == "Behind");

        var overallPerformancePct = kpisWithEntries == 0
            ? 0
            : (int)Math.Round((double)onTrackCount / kpisWithEntries * 100);

        var activeGoals = await _db.StrategicGoals
            .CountAsync(g => g.Status == "In Progress", ct);

        var departmentsTracked = await _db.Kpis
            .Where(k => k.IsActive)
            .Select(k => k.DepartmentId)
            .Distinct()
            .CountAsync(ct);

        // BSC perspectives: group latest-entry KPIs by perspective
        var latestKpiIds = latestByKpi.Select(e => e.KpiId).ToList();
        var latestKpisWithPerspective = await _db.Kpis
            .Where(k => latestKpiIds.Contains(k.Id))
            .Select(k => new { k.Id, k.Perspective })
            .ToListAsync(ct);

        var latestByKpiDict = latestByKpi.ToDictionary(e => e.KpiId);

        var bscPerspectives = latestKpisWithPerspective
            .GroupBy(k => k.Perspective)
            .Select(g => new BscPerspectiveViewModel(
                g.Key,
                g.Count(k => latestByKpiDict.TryGetValue(k.Id, out var e) && e.Status == "On Track"),
                g.Count(k => latestByKpiDict.TryGetValue(k.Id, out var e) && e.Status == "At Risk"),
                g.Count(k => latestByKpiDict.TryGetValue(k.Id, out var e) && e.Status == "Behind")))
            .ToList();

        // Top departments by performance score
        var activeKpisWithDept = await _db.Kpis
            .Where(k => k.IsActive)
            .Include(k => k.Department)
            .ToListAsync(ct);

        var topDepartments = activeKpisWithDept
            .GroupBy(k => k.Department.Name)
            .Select(g =>
            {
                var deptKpisWithEntries = g.Where(k => latestByKpiDict.ContainsKey(k.Id)).ToList();
                var deptOnTrack = deptKpisWithEntries.Count(k => latestByKpiDict[k.Id].Status == "On Track");
                var scorePct = deptKpisWithEntries.Count == 0
                    ? 0
                    : (int)Math.Round((double)deptOnTrack / deptKpisWithEntries.Count * 100);
                return new DeptPerformanceViewModel(g.Key, scorePct);
            })
            .OrderByDescending(d => d.ScorePct)
            .ToList();

        // Underperforming KPIs (latest status == "Behind")
        var behindKpiIds = latestByKpi
            .Where(e => e.Status == "Behind")
            .Select(e => e.KpiId)
            .ToList();

        var underperformingKpis = await _db.Kpis
            .Where(k => k.IsActive && behindKpiIds.Contains(k.Id))
            .Include(k => k.Department)
            .Select(k => new
            {
                k.Id,
                k.Name,
                DeptName = k.Department.Name,
                k.Target,
                k.Unit
            })
            .ToListAsync(ct);

        var underperformingList = underperformingKpis.Select(k =>
        {
            var latest = latestByKpiDict[k.Id];
            return new UnderperformingKpiViewModel(
                k.Name,
                k.DeptName,
                FormatValue(k.Target, k.Unit),
                FormatValue(latest.ActualValue, k.Unit));
        }).ToList();

        return View("Dashboard", new ExecutiveDashboardViewModel
        {
            OverallPerformancePct = overallPerformancePct,
            TotalKpis             = totalKpis,
            ActiveGoals           = activeGoals,
            DepartmentsTracked    = departmentsTracked,
            BscPerspectives       = bscPerspectives,
            ChartOnTrack          = onTrackCount,
            ChartAtRisk           = atRiskCount,
            ChartBehind           = behindCount,
            TopDepartments        = topDepartments,
            UnderperformingKpis   = underperformingList
        });
    }

    // ── KPI Tracking ──────────────────────────────────────────────────────────
    public async Task<IActionResult> KPITracking(
        string? department,
        string? perspective,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Manager", "User", "Executive")) return Forbid();
        ViewData["Title"] = "KPI Tracking";

        var selectedDepartment  = string.IsNullOrWhiteSpace(department)  ? "All" : department.Trim();
        var selectedPerspective = string.IsNullOrWhiteSpace(perspective) ? "All" : perspective.Trim();
        var selectedStatus      = string.IsNullOrWhiteSpace(status)      ? "All" : status.Trim();

        var kpisQuery = _db.Kpis
            .Where(k => k.IsActive)
            .Include(k => k.Department)
            .Include(k => k.LogEntries)
            .AsNoTracking();

        if (selectedDepartment  != "All") kpisQuery = kpisQuery.Where(k => k.Department.Name == selectedDepartment);
        if (selectedPerspective != "All") kpisQuery = kpisQuery.Where(k => k.Perspective     == selectedPerspective);

        var kpis = await kpisQuery.ToListAsync(cancellationToken);

        var kpiItems = kpis
            .Select(k =>
            {
                var latest    = k.LogEntries.OrderByDescending(e => e.LoggedAt).FirstOrDefault();
                var kpiStatus = latest?.Status ?? "No Data";
                var severity  = kpiStatus == "Behind"  ? AlertSeverity.Critical
                              : kpiStatus == "At Risk"  ? AlertSeverity.Warning
                              : AlertSeverity.Standard;

                return new KpiTrackingItemViewModel
                {
                    Name        = k.Name,
                    Department  = k.Department.Name,
                    Perspective = k.Perspective,
                    Target      = FormatValue(k.Target, k.Unit),
                    Actual      = latest != null ? FormatValue(latest.ActualValue, k.Unit) : "—",
                    Status      = kpiStatus,
                    Severity    = severity
                };
            })
            .Where(k => selectedStatus == "All" || k.Status == selectedStatus)
            .ToList();

        var allDepts  = await _db.Kpis.Where(k => k.IsActive).Include(k => k.Department)
                            .Select(k => k.Department.Name).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);
        var allPersps = await _db.Kpis.Where(k => k.IsActive)
                            .Select(k => k.Perspective).Distinct().OrderBy(x => x).ToListAsync(cancellationToken);

        return View(new KpiTrackingPageViewModel
        {
            Kpis                = kpiItems,
            Departments         = allDepts,
            Perspectives        = allPersps,
            SelectedDepartment  = selectedDepartment,
            SelectedPerspective = selectedPerspective,
            SelectedStatus      = selectedStatus,
            IsFallback          = false,
            DataMessage         = string.Empty
        });
    }

    // ── KPI Log Entry ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> KPILogEntry(CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Manager", "User")) return Forbid();
        ViewData["Title"] = "KPI Log Entry";
        ViewBag.Kpis = await GetKpiDetailsAsync(cancellationToken);
        return View(new KpiLogEntryViewModel { LoggedAt = DateTime.Today });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KPILogEntry(KpiLogEntryViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "KPI Log Entry";
        ViewBag.Kpis = await GetKpiDetailsAsync(cancellationToken);

        if (!ModelState.IsValid)
            return View(model);

        var kpi = await _db.Kpis.FindAsync(new object[] { model.KpiId }, cancellationToken);
        if (kpi is null)
        {
            ModelState.AddModelError(nameof(model.KpiId), "Selected KPI not found.");
            return View(model);
        }

        var computedStatus = ComputeStatus(kpi, model.ActualValue);
        var userId         = HttpContext.Session.GetInt32(SessionUserId) ?? 1;

        _db.KpiLogEntries.Add(new KpiLogEntry
        {
            KpiId          = model.KpiId,
            LoggedByUserId = userId,
            ActualValue    = model.ActualValue,
            Status         = computedStatus,
            Notes          = model.Notes?.Trim(),
            LoggedAt       = model.LoggedAt.ToUniversalTime(),
            Period         = model.Period
        });

        if (computedStatus != "On Track")
        {
            _db.Notifications.Add(new Notification
            {
                UserId    = userId,
                Title     = $"{kpi.Name} is {computedStatus}",
                Message   = $"Actual value {FormatValue(model.ActualValue, kpi.Unit)} is below the target of {FormatValue(kpi.Target, kpi.Unit)} for {model.Period}.",
                Severity  = computedStatus == "Behind" ? "Critical" : "Warning",
                Icon      = computedStatus == "Behind" ? "bi-x-circle" : "bi-exclamation-triangle",
                IsRead    = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = userId,
            Action     = "Logged KPI Entry",
            EntityType = "KpiLogEntry",
            Details    = $"{kpi.Name} — Actual: {model.ActualValue} {kpi.Unit} | Status: {computedStatus} | Period: {model.Period}",
            OccurredAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Entry saved. <strong>{kpi.Name}</strong> is <strong>{computedStatus}</strong> for {model.Period}.";
        return RedirectToAction(nameof(KPITracking));
    }

    [HttpGet]
    public async Task<IActionResult> KpiDetail(int id, CancellationToken cancellationToken)
    {
        var kpi = await _db.Kpis
            .Where(k => k.Id == id && k.IsActive)
            .Include(k => k.Department)
            .Select(k => new KpiDetailDto
            {
                Id          = k.Id,
                Name        = k.Name,
                Department  = k.Department.Name,
                Perspective = k.Perspective,
                Target      = k.Target,
                Unit        = k.Unit
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (kpi is null) return NotFound();
        return Json(kpi);
    }

    // ── Notifications ─────────────────────────────────────────────────────────
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Notifications";

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 0;

        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationItemViewModel
            {
                Title    = n.Title,
                Message  = n.Message,
                Time     = ToRelativeTime(n.CreatedAt),
                Read     = n.IsRead,
                Icon     = n.Icon,
                Severity = n.Severity == "Critical" ? AlertSeverity.Critical
                         : n.Severity == "Warning"  ? AlertSeverity.Warning
                         : AlertSeverity.Standard
            })
            .ToListAsync(cancellationToken);

        return View(new NotificationsPageViewModel
        {
            Notifications = notifications,
            IsFallback    = false,
            DataMessage   = string.Empty
        });
    }

    // ── KPI Management (Admin / Manager only) ────────────────────────────────
    public async Task<IActionResult> KpiManagement(CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "KPI Management";

        if (!CanManageKpis())
            return RedirectToAction(nameof(Dashboard));

        var kpis = await _db.Kpis
            .Include(k => k.Department)
            .Include(k => k.LogEntries)
            .OrderBy(k => k.Department.Name).ThenBy(k => k.Name)
            .Select(k => new KpiManagementItemViewModel
            {
                Id          = k.Id,
                Name        = k.Name,
                Department  = k.Department.Name,
                Perspective = k.Perspective,
                Target      = k.Target,
                Unit        = k.Unit,
                IsActive    = k.IsActive,
                LogCount    = k.LogEntries.Count,
                CreatedAt   = k.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return View(new KpiManagementListViewModel { Kpis = kpis });
    }

    [HttpGet]
    public async Task<IActionResult> KpiCreate(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Create KPI";
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));
        return View("KpiForm", await BuildKpiFormAsync(new KpiFormViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KpiCreate(KpiFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Create KPI";
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));

        if (!ModelState.IsValid)
            return View("KpiForm", await BuildKpiFormAsync(model, cancellationToken));

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        var kpi = new Kpi
        {
            Name         = model.Name.Trim(),
            DepartmentId = model.DepartmentId,
            Perspective  = model.Perspective,
            Unit         = model.Unit.Trim(),
            Target       = model.Target,
            Description  = model.Description?.Trim(),
            IsActive     = model.IsActive,
            CreatedAt    = DateTime.UtcNow
        };
        _db.Kpis.Add(kpi);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "Created KPI", EntityType = "Kpi",
            Details = $"{model.Name} — Target: {model.Target} {model.Unit}", OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"KPI <strong>{kpi.Name}</strong> created successfully.";
        return RedirectToAction(nameof(KpiManagement));
    }

    [HttpGet]
    public async Task<IActionResult> KpiEdit(int id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Edit KPI";
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));

        var kpi = await _db.Kpis.FindAsync(new object[] { id }, cancellationToken);
        if (kpi is null) return NotFound();

        var form = new KpiFormViewModel
        {
            Id = kpi.Id, Name = kpi.Name, DepartmentId = kpi.DepartmentId,
            Perspective = kpi.Perspective, Unit = kpi.Unit, Target = kpi.Target,
            Description = kpi.Description, IsActive = kpi.IsActive
        };
        return View("KpiForm", await BuildKpiFormAsync(form, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KpiEdit(KpiFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Edit KPI";
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));

        if (!ModelState.IsValid)
            return View("KpiForm", await BuildKpiFormAsync(model, cancellationToken));

        var kpi = await _db.Kpis.FindAsync(new object[] { model.Id }, cancellationToken);
        if (kpi is null) return NotFound();

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        var oldTarget = kpi.Target;

        kpi.Name = model.Name.Trim(); kpi.DepartmentId = model.DepartmentId;
        kpi.Perspective = model.Perspective; kpi.Unit = model.Unit.Trim();
        kpi.Target = model.Target; kpi.Description = model.Description?.Trim();
        kpi.IsActive = model.IsActive;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "Updated KPI", EntityType = "Kpi", EntityId = kpi.Id,
            Details = oldTarget != model.Target
                ? $"{kpi.Name} — Target changed from {oldTarget} to {model.Target} {model.Unit}"
                : $"{kpi.Name} updated.",
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = $"KPI <strong>{kpi.Name}</strong> updated successfully.";
        return RedirectToAction(nameof(KpiManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KpiToggleActive(int id, CancellationToken cancellationToken)
    {
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));

        var kpi = await _db.Kpis.FindAsync(new object[] { id }, cancellationToken);
        if (kpi is null) return NotFound();

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        kpi.IsActive = !kpi.IsActive;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = kpi.IsActive ? "Activated KPI" : "Deactivated KPI",
            EntityType = "Kpi", EntityId = kpi.Id,
            Details = $"{kpi.Name} set to {(kpi.IsActive ? "Active" : "Inactive")}.", OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(KpiManagement));
    }

    // ── Other pages ───────────────────────────────────────────────────────────
    public async Task<IActionResult> BalancedScorecard(CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Manager", "User", "Executive")) return Forbid();
        ViewData["Title"] = "Balanced Scorecards";

        var kpis = await _db.Kpis
            .Where(k => k.IsActive)
            .Include(k => k.LogEntries)
            .OrderBy(k => k.Perspective).ThenBy(k => k.Name)
            .ToListAsync(cancellationToken);

        var perspectiveOrder = new[] { "Financial", "Customer", "Internal Process", "Learning & Growth" };

        var groups = perspectiveOrder
            .Select(p =>
            {
                var kpisInPerspective = kpis.Where(k => k.Perspective == p).ToList();
                var rows = kpisInPerspective.Select(k =>
                {
                    var latest = k.LogEntries.OrderByDescending(e => e.LoggedAt).FirstOrDefault();
                    return new ScorecardKpiRowViewModel
                    {
                        Name   = k.Name,
                        Target = FormatValue(k.Target, k.Unit),
                        Actual = latest != null ? FormatValue(latest.ActualValue, k.Unit) : "—",
                        Status = latest?.Status ?? "No Data"
                    };
                }).ToList();

                return new ScorecardPerspectiveGroupViewModel
                {
                    Perspective = p,
                    Kpis        = rows
                };
            })
            .Where(g => g.Kpis.Count > 0)
            .ToList();

        return View(new BalancedScorecardViewModel { Perspectives = groups });
    }

    public async Task<IActionResult> PerformanceAnalytics(
        string? department,
        int months = 6,
        CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Manager", "Executive")) return Forbid();
        ViewData["Title"] = "Performance Analytics";

        var selectedDept   = string.IsNullOrWhiteSpace(department) ? "All" : department.Trim();
        var selectedMonths = months is 3 or 6 or 12 ? months : 6;

        var allDepts = await _db.Departments.OrderBy(d => d.Name).Select(d => d.Name).ToListAsync(cancellationToken);

        // Date range
        var now    = DateTime.UtcNow;
        var cutoff = new DateTime(now.AddMonths(-(selectedMonths - 1)).Year,
                                  now.AddMonths(-(selectedMonths - 1)).Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthSlots  = Enumerable.Range(0, selectedMonths).Select(i => cutoff.AddMonths(i)).ToList();
        var trendLabels = monthSlots.Select(m => m.ToString("MMM yyyy")).ToList();

        // Trend: group by (perspective, year, month) → average actual value
        var entriesQuery = _db.KpiLogEntries
            .Where(e => e.LoggedAt >= cutoff)
            .Include(e => e.Kpi).ThenInclude(k => k.Department)
            .AsQueryable();

        if (selectedDept != "All")
            entriesQuery = entriesQuery.Where(e => e.Kpi.Department.Name == selectedDept);

        var entries = await entriesQuery.ToListAsync(cancellationToken);

        var perspectiveColors = new Dictionary<string, string>
        {
            ["Financial"]         = "#1B4FD8",
            ["Customer"]          = "#0891b2",
            ["Internal Process"]  = "#7c3aed",
            ["Learning & Growth"] = "#059669"
        };

        var trendGrouped = entries
            .GroupBy(e => (Perspective: e.Kpi.Perspective, Year: e.LoggedAt.Year, Month: e.LoggedAt.Month))
            .ToDictionary(g => g.Key, g => (double)g.Average(e => e.ActualValue));

        var perspectives = perspectiveColors.Keys.ToList();
        var trendDatasets = perspectives.Select(p =>
        {
            var values = monthSlots.Select(slot =>
            {
                var key = (Perspective: p, Year: slot.Year, Month: slot.Month);
                return trendGrouped.TryGetValue(key, out var avg) ? (decimal?)Math.Round((decimal)avg, 2) : null;
            }).ToList();
            return new TrendDatasetViewModel(p, values);
        }).Where(d => d.Values.Any(v => v.HasValue)).ToList();

        // Bar chart: department performance scores (% On Track from latest entries)
        var latestByKpi = await _db.KpiLogEntries
            .GroupBy(e => e.KpiId)
            .Select(g => g.OrderByDescending(e => e.LoggedAt).First())
            .ToListAsync(cancellationToken);

        var latestDict = latestByKpi.ToDictionary(e => e.KpiId);

        var kpisWithDept = await _db.Kpis
            .Where(k => k.IsActive)
            .Include(k => k.Department)
            .ToListAsync(cancellationToken);

        var barData = kpisWithDept
            .GroupBy(k => k.Department.Name)
            .Select(g =>
            {
                var withEntries = g.Where(k => latestDict.ContainsKey(k.Id)).ToList();
                var onTrack     = withEntries.Count(k => latestDict[k.Id].Status == "On Track");
                var score       = withEntries.Count == 0 ? 0 : (int)Math.Round((double)onTrack / withEntries.Count * 100);
                return (Name: g.Key, Score: score);
            })
            .OrderByDescending(d => d.Score)
            .ToList();

        // Doughnut
        var onTrackCount = latestByKpi.Count(e => e.Status == "On Track");
        var atRiskCount  = latestByKpi.Count(e => e.Status == "At Risk");
        var behindCount  = latestByKpi.Count(e => e.Status == "Behind");

        return View(new PerformanceAnalyticsViewModel
        {
            SelectedDepartment = selectedDept,
            SelectedMonths     = selectedMonths,
            Departments        = allDepts,
            TrendLabels        = trendLabels,
            TrendDatasets      = trendDatasets,
            BarLabels          = barData.Select(d => d.Name).ToList(),
            BarScores          = barData.Select(d => d.Score).ToList(),
            DoughnutOnTrack    = onTrackCount,
            DoughnutAtRisk     = atRiskCount,
            DoughnutBehind     = behindCount
        });
    }

    public async Task<IActionResult> StrategicPlanning(CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Manager", "Executive")) return Forbid();
        ViewData["Title"] = "Strategic Planning";

        var goals = await _db.StrategicGoals
            .Include(g => g.Owner)
            .OrderBy(g => g.Status).ThenBy(g => g.Title)
            .Select(g => new StrategicGoalCardViewModel
            {
                Id          = g.Id,
                Title       = g.Title,
                Description = g.Description,
                Perspective = g.Perspective,
                Status      = g.Status,
                DueDate     = g.DueDate.HasValue ? g.DueDate.Value.ToString("MMM d, yyyy") : null,
                OwnerName   = g.Owner != null ? g.Owner.FullName : null
            })
            .ToListAsync(cancellationToken);

        return View(new StrategicPlanningViewModel { Goals = goals });
    }

    [HttpGet]
    public IActionResult StrategicGoalCreate()
    {
        if (!HasAccess("Admin", "Manager")) return Forbid();
        ViewData["Title"] = "Add Strategic Goal";
        return View("StrategicGoalForm", new StrategicGoalFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StrategicGoalCreate(StrategicGoalFormViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Manager")) return Forbid();
        ViewData["Title"] = "Add Strategic Goal";

        if (!ModelState.IsValid)
            return View("StrategicGoalForm", model);

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        var goal = new PeakMetrics.Web.Models.StrategicGoal
        {
            Title       = model.Title.Trim(),
            Description = model.Description?.Trim(),
            Perspective = model.Perspective,
            Status      = model.Status,
            DueDate     = model.DueDate.HasValue ? model.DueDate.Value.ToUniversalTime() : null,
            OwnerUserId = userId,
            CreatedAt   = DateTime.UtcNow
        };
        _db.StrategicGoals.Add(goal);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "Created Strategic Goal", EntityType = "StrategicGoal",
            Details = goal.Title, OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Goal <strong>{goal.Title}</strong> created successfully.";
        return RedirectToAction(nameof(StrategicPlanning));
    }

    [HttpGet]
    public async Task<IActionResult> StrategicGoalEdit(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Manager")) return Forbid();
        ViewData["Title"] = "Edit Strategic Goal";

        var goal = await _db.StrategicGoals.FindAsync(new object[] { id }, cancellationToken);
        if (goal is null) return NotFound();

        return View("StrategicGoalForm", new StrategicGoalFormViewModel
        {
            Id          = goal.Id,
            Title       = goal.Title,
            Description = goal.Description,
            Perspective = goal.Perspective,
            Status      = goal.Status,
            DueDate     = goal.DueDate.HasValue ? goal.DueDate.Value.ToLocalTime() : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StrategicGoalEdit(StrategicGoalFormViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Manager")) return Forbid();
        ViewData["Title"] = "Edit Strategic Goal";

        if (!ModelState.IsValid)
            return View("StrategicGoalForm", model);

        var goal = await _db.StrategicGoals.FindAsync(new object[] { model.Id }, cancellationToken);
        if (goal is null) return NotFound();

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        goal.Title       = model.Title.Trim();
        goal.Description = model.Description?.Trim();
        goal.Perspective = model.Perspective;
        goal.Status      = model.Status;
        goal.DueDate     = model.DueDate.HasValue ? model.DueDate.Value.ToUniversalTime() : null;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "Updated Strategic Goal", EntityType = "StrategicGoal", EntityId = goal.Id,
            Details = goal.Title, OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Goal <strong>{goal.Title}</strong> updated.";
        return RedirectToAction(nameof(StrategicPlanning));
    }

    public async Task<IActionResult> ExecutiveReporting(CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Manager", "Executive")) return Forbid();
        ViewData["Title"] = "Executive Reporting";

        // Build available periods from actual log entries
        var periods = await _db.KpiLogEntries
            .Select(e => e.Period)
            .Distinct()
            .OrderByDescending(p => p)
            .ToListAsync(cancellationToken);

        var selectedPeriod = periods.FirstOrDefault() ?? string.Empty;

        // Latest entry per KPI (for overall stats)
        var latestByKpi = await _db.KpiLogEntries
            .GroupBy(e => e.KpiId)
            .Select(g => g.OrderByDescending(e => e.LoggedAt).First())
            .ToListAsync(cancellationToken);

        var totalKpis = await _db.Kpis.CountAsync(k => k.IsActive, cancellationToken);
        var onTrack   = latestByKpi.Count(e => e.Status == "On Track");
        var atRisk    = latestByKpi.Count(e => e.Status == "At Risk");
        var behind    = latestByKpi.Count(e => e.Status == "Behind");
        var noData    = totalKpis - latestByKpi.Count;
        var kpisWithEntries = latestByKpi.Count;
        var overallPct = kpisWithEntries == 0 ? 0 : (int)Math.Round((double)onTrack / kpisWithEntries * 100);

        // KPI rows with latest values
        var kpiRows = await _db.Kpis
            .Where(k => k.IsActive)
            .Include(k => k.Department)
            .Include(k => k.LogEntries)
            .OrderBy(k => k.Department.Name).ThenBy(k => k.Name)
            .ToListAsync(cancellationToken);

        var latestDict = latestByKpi.ToDictionary(e => e.KpiId);

        var execKpis = kpiRows.Select(k =>
        {
            latestDict.TryGetValue(k.Id, out var latest);
            var actualVal  = latest != null ? latest.ActualValue : (decimal?)null;
            var statusStr  = latest?.Status ?? "No Data";
            var isWholeUnit = k.Unit.Equals("days", StringComparison.OrdinalIgnoreCase)
                           || k.Unit.Equals("hrs",  StringComparison.OrdinalIgnoreCase)
                           || k.Unit.Equals("count",StringComparison.OrdinalIgnoreCase);
            var variance   = actualVal.HasValue
                ? $"{(actualVal.Value >= k.Target ? "+" : "")}{(isWholeUnit ? $"{actualVal.Value - k.Target:F0}" : $"{actualVal.Value - k.Target:F2}")} {k.Unit}"
                : "—";

            return new ExecKpiRowViewModel
            {
                Name       = k.Name,
                Department = k.Department.Name,
                Target     = FormatValue(k.Target, k.Unit),
                Actual     = actualVal.HasValue ? FormatValue(actualVal.Value, k.Unit) : "—",
                Status     = statusStr,
                Variance   = variance
            };
        }).ToList();

        // Scorecard by perspective
        var latestKpiIds = latestByKpi.Select(e => e.KpiId).ToList();
        var kpisWithPerspective = await _db.Kpis
            .Where(k => k.IsActive)
            .Select(k => new { k.Id, k.Perspective })
            .ToListAsync(cancellationToken);

        var perspectiveOrder = new[] { "Financial", "Customer", "Internal Process", "Learning & Growth" };
        var scorecards = perspectiveOrder.Select(p =>
        {
            var inPerspective = kpisWithPerspective.Where(k => k.Perspective == p).ToList();
            var withEntries   = inPerspective.Where(k => latestDict.ContainsKey(k.Id)).ToList();
            return new ExecScorecardRowViewModel
            {
                Perspective = p,
                OnTrack     = withEntries.Count(k => latestDict[k.Id].Status == "On Track"),
                AtRisk      = withEntries.Count(k => latestDict[k.Id].Status == "At Risk"),
                Behind      = withEntries.Count(k => latestDict[k.Id].Status == "Behind"),
                Total       = inPerspective.Count
            };
        }).Where(s => s.Total > 0).ToList();

        // Strategic goals
        var goalRows = await _db.StrategicGoals
            .OrderBy(g => g.Status).ThenBy(g => g.Title)
            .Select(g => new ExecGoalRowViewModel
            {
                Title   = g.Title,
                Status  = g.Status,
                DueDate = g.DueDate.HasValue ? g.DueDate.Value.ToString("MMM d, yyyy") : null
            })
            .ToListAsync(cancellationToken);

        return View(new ExecutiveReportingViewModel
        {
            TotalKpis        = totalKpis,
            OnTrack          = onTrack,
            AtRisk           = atRisk,
            Behind           = behind,
            NoData           = noData,
            OverallPct       = overallPct,
            Kpis             = execKpis,
            Scorecards       = scorecards,
            Goals            = goalRows,
            SelectedPeriod   = selectedPeriod,
            AvailablePeriods = periods
        });
    }

    public IActionResult Profile()              { ViewData["Title"] = "Profile"; return View(); }

    public IActionResult AccessDenied()
    {
        ViewData["Title"] = "Access Denied";
        return View();
    }

    public async Task<IActionResult> DepartmentManagement(CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData["Title"] = "Department Management";

        var departments = await _db.Departments
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentRowViewModel
            {
                Id          = d.Id,
                Name        = d.Name,
                Description = d.Description,
                UserCount   = d.Users.Count,
                KpiCount    = d.Kpis.Count(k => k.IsActive),
                CreatedAt   = d.CreatedAt.ToString("MMM d, yyyy")
            })
            .ToListAsync(cancellationToken);

        return View(new DepartmentManagementViewModel { Departments = departments });
    }

    [HttpGet]
    public async Task<IActionResult> DepartmentCreate()
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData["Title"] = "Add Department";
        return View("DepartmentForm", new DepartmentFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentCreate(DepartmentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData["Title"] = "Add Department";

        if (!ModelState.IsValid)
            return View("DepartmentForm", model);

        var exists = await _db.Departments.AnyAsync(d => d.Name == model.Name.Trim(), cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Name), "A department with this name already exists.");
            return View("DepartmentForm", model);
        }

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        var dept = new PeakMetrics.Web.Models.Department
        {
            Name        = model.Name.Trim(),
            Description = model.Description?.Trim(),
            CreatedAt   = DateTime.UtcNow
        };
        _db.Departments.Add(dept);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "Created Department", EntityType = "Department",
            Details = dept.Name, OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Department <strong>{dept.Name}</strong> created successfully.";
        return RedirectToAction(nameof(DepartmentManagement));
    }

    [HttpGet]
    public async Task<IActionResult> DepartmentEdit(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData["Title"] = "Edit Department";

        var dept = await _db.Departments.FindAsync(new object[] { id }, cancellationToken);
        if (dept is null) return NotFound();

        return View("DepartmentForm", new DepartmentFormViewModel
        {
            Id          = dept.Id,
            Name        = dept.Name,
            Description = dept.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentEdit(DepartmentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData["Title"] = "Edit Department";

        if (!ModelState.IsValid)
            return View("DepartmentForm", model);

        var dept = await _db.Departments.FindAsync(new object[] { model.Id }, cancellationToken);
        if (dept is null) return NotFound();

        var duplicate = await _db.Departments
            .AnyAsync(d => d.Name == model.Name.Trim() && d.Id != model.Id, cancellationToken);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.Name), "A department with this name already exists.");
            return View("DepartmentForm", model);
        }

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        dept.Name        = model.Name.Trim();
        dept.Description = model.Description?.Trim();

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "Updated Department", EntityType = "Department", EntityId = dept.Id,
            Details = dept.Name, OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Department <strong>{dept.Name}</strong> updated successfully.";
        return RedirectToAction(nameof(DepartmentManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentDelete(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();

        var dept = await _db.Departments
            .Include(d => d.Users)
            .Include(d => d.Kpis)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (dept is null) return NotFound();

        if (dept.Users.Count > 0 || dept.Kpis.Count > 0)
        {
            TempData["ErrorMessage"] = $"Cannot delete <strong>{dept.Name}</strong> — it has {dept.Users.Count} user(s) and {dept.Kpis.Count} KPI(s) assigned to it.";
            return RedirectToAction(nameof(DepartmentManagement));
        }

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "Deleted Department", EntityType = "Department", EntityId = dept.Id,
            Details = dept.Name, OccurredAt = DateTime.UtcNow
        });
        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = $"Department <strong>{dept.Name}</strong> deleted.";
        return RedirectToAction(nameof(DepartmentManagement));
    }

    public async Task<IActionResult> AuditLog(CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin")) return Forbid();
        ViewData["Title"] = "Audit Log";

        var entries = await _db.AuditLogs
            .Include(a => a.User)
            .OrderByDescending(a => a.OccurredAt)
            .Take(200)
            .Select(a => new AuditLogEntryViewModel
            {
                Timestamp  = a.OccurredAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                UserName   = a.User != null ? a.User.FullName : "System",
                Action     = a.Action,
                EntityType = a.EntityType,
                Details    = a.Details ?? string.Empty
            })
            .ToListAsync(cancellationToken);

        return View(entries);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<List<KpiDetailDto>> GetKpiDetailsAsync(CancellationToken ct) =>
        await _db.Kpis
            .Where(k => k.IsActive)
            .Include(k => k.Department)
            .OrderBy(k => k.Department.Name).ThenBy(k => k.Name)
            .Select(k => new KpiDetailDto
            {
                Id          = k.Id,
                Name        = k.Name,
                Department  = k.Department.Name,
                Perspective = k.Perspective,
                Target      = k.Target,
                Unit        = k.Unit
            })
            .ToListAsync(ct);

    private async Task<KpiFormViewModel> BuildKpiFormAsync(KpiFormViewModel model, CancellationToken ct)
    {
        model.Departments = await _db.Departments
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentOptionViewModel { Id = d.Id, Name = d.Name })
            .ToListAsync(ct);
        return model;
    }

    private bool CanManageKpis()
    {
        var role = HttpContext.Session.GetString(SessionUserRole) ?? string.Empty;
        return role is "Admin" or "Administrator" or "Manager";
    }

    /// <summary>Returns true if the current session role is in the allowed list.</summary>
    private bool HasAccess(params string[] allowedRoles)
    {
        var role = HttpContext.Session.GetString(SessionUserRole) ?? string.Empty;
        return allowedRoles.Contains(role);
    }

    private static string ComputeStatus(Kpi kpi, decimal actual)
    {
        var lowerIsBetter = kpi.Unit == "days"
            || kpi.Name.Contains("Turnover", StringComparison.OrdinalIgnoreCase)
            || kpi.Name.Contains("Defect",   StringComparison.OrdinalIgnoreCase)
            || kpi.Name.Contains("Cycle",    StringComparison.OrdinalIgnoreCase);

        if (lowerIsBetter)
        {
            if (actual <= kpi.Target)         return "On Track";
            if (actual <= kpi.Target * 1.25m) return "At Risk";
            return "Behind";
        }

        if (actual >= kpi.Target)             return "On Track";
        if (actual >= kpi.Target * 0.85m)     return "At Risk";
        return "Behind";
    }

    private static string ToRelativeTime(DateTime utc)
    {
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalMinutes < 1)  return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays    < 7)  return $"{(int)diff.TotalDays}d ago";
        return utc.ToLocalTime().ToString("MMM d, yyyy");
    }

    /// <summary>
    /// Formats a decimal value with 2 decimal places for most units,
    /// or 0 decimal places for whole-number units like days and hrs.
    /// Currency codes get their symbol as a prefix instead of a suffix.
    /// </summary>
    private static string FormatValue(decimal value, string unit)
    {
        var currencySymbols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PHP"] = "₱", ["USD"] = "$", ["EUR"] = "€", ["GBP"] = "£",
            ["JPY"] = "¥", ["CNY"] = "¥", ["AUD"] = "A$", ["CAD"] = "C$",
            ["SGD"] = "S$", ["HKD"] = "HK$", ["KRW"] = "₩", ["INR"] = "₹",
            ["MYR"] = "RM", ["THB"] = "฿", ["IDR"] = "Rp", ["VND"] = "₫",
            ["SAR"] = "﷼", ["AED"] = "د.إ", ["CHF"] = "Fr", ["BRL"] = "R$"
        };

        if (currencySymbols.TryGetValue(unit, out var symbol))
            return $"{symbol}{value:N2}";

        var isWholeUnit = unit.Equals("days", StringComparison.OrdinalIgnoreCase)
                       || unit.Equals("hrs",  StringComparison.OrdinalIgnoreCase)
                       || unit.Equals("count",StringComparison.OrdinalIgnoreCase);

        return isWholeUnit
            ? $"{value:F0} {unit}"
            : $"{value:F2} {unit}";
    }
}
