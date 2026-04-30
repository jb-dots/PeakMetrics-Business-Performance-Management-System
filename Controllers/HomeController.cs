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

    // ── ViewData / TempData key constants ─────────────────────────────────────
    private const string VdTitle          = "Title";
    private const string TdSuccessMessage = "SuccessMessage";

    // ── Role constants ────────────────────────────────────────────────────────
    private const string RoleAdmin         = "Admin";
    private const string RoleAdministrator = "Administrator";
    private const string RoleManager       = "Manager";
    private const string RoleExecutive     = "Executive";

    // ── View name constants ───────────────────────────────────────────────────
    private const string ViewDashboard         = "Dashboard";
    private const string ViewKpiForm           = "KpiForm";
    private const string ViewStrategicGoalForm = "StrategicGoalForm";
    private const string ViewDepartmentForm    = "DepartmentForm";
    private const string ViewUserForm          = "UserForm";

    // ── Entity type constants ─────────────────────────────────────────────────
    private const string EntityAppUser    = "AppUser";
    private const string EntityDepartment = "Department";

    // ── Status constants ──────────────────────────────────────────────────────
    private const string StatusOnTrack = "On Track";
    private const string StatusAtRisk  = "At Risk";
    private const string StatusBehind  = "Behind";
    private const string StatusNoData  = "No Data";

    // ── Date format constants ─────────────────────────────────────────────────
    private const string DateFormatShort = "MMM d, yyyy";

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
            await PopulateQuickNotificationsAsync(userId, isNotificationsPage, context.HttpContext.RequestAborted);
        }

        await next();
    }

    private async Task PopulateQuickNotificationsAsync(int userId, bool isNotificationsPage, CancellationToken ct)
    {
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
                    Severity = ToAlertSeverity(n.Severity)
                })
                .ToListAsync(ct);

            ViewData["QuickNotifications"]       = quickNotifs;
            ViewData["UnreadQuickNotifications"] = quickNotifs.Count(n => !n.Read);
        }
        else
        {
            ViewData["QuickNotifications"]       = new List<NotificationItemViewModel>();
            ViewData["UnreadQuickNotifications"] = 0;
        }
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    public IActionResult Index() => RedirectToAction(nameof(Login));

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetInt32(SessionUserId) is not null)
            return RedirectToAction(nameof(Dashboard));

        ViewData[VdTitle] = "Login";
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        ViewData[VdTitle] = "Login";

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
            EntityType = EntityAppUser,
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
                EntityType = EntityAppUser,
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
        ViewData[VdTitle] = ViewDashboard;

        var role = HttpContext.Session.GetString(SessionUserRole) ?? string.Empty;
        return role switch
        {
            RoleAdmin         => await BuildSuperAdminDashboardAsync(cancellationToken),
            RoleAdministrator => await BuildAdminDashboardAsync(cancellationToken),
            RoleManager       => await BuildManagerDashboardAsync(cancellationToken),
            "User"            => await BuildStaffDashboardAsync(cancellationToken),
            RoleExecutive     => await BuildExecutiveDashboardAsync(cancellationToken),
            _                 => RedirectToAction(nameof(Login))
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

        var departments = await GetDepartmentOverviewAsync(ct);

        return View(ViewDashboard, new SuperAdminDashboardViewModel
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

        var loggedKpiIdsThisMonth = await GetLoggedKpiIdsThisMonthAsync(ct);

        var pendingKpis = await _db.Kpis
            .CountAsync(k => k.IsActive && !loggedKpiIdsThisMonth.Contains(k.Id), ct);

        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
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
                u.CreatedAt.ToString(DateFormatShort)))
            .ToListAsync(ct);

        var departments = await GetDepartmentOverviewAsync(ct);

        return View(ViewDashboard, new AdministratorDashboardViewModel
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
        var latestByKpi = await GetLatestEntriesPerKpiAsync(ct);

        var totalKpis = await _db.Kpis.CountAsync(k => k.IsActive, ct);
        var onTrack   = latestByKpi.Count(e => e.Status == StatusOnTrack);
        var atRisk    = latestByKpi.Count(e => e.Status == StatusAtRisk);
        var behind    = latestByKpi.Count(e => e.Status == StatusBehind);

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
                var deptOnTrack = g.Count(k => latestByKpiId.TryGetValue(k.Id, out var e) && e.Status == StatusOnTrack);
                var deptAtRisk  = g.Count(k => latestByKpiId.TryGetValue(k.Id, out var e) && e.Status == StatusAtRisk);
                var deptBehind  = g.Count(k => latestByKpiId.TryGetValue(k.Id, out var e) && e.Status == StatusBehind);
                return new DeptKpiStatusViewModel(g.Key, deptOnTrack, deptAtRisk, deptBehind);
            })
            .ToList();

        // Trend data: last 6 calendar months
        var now         = DateTime.UtcNow;
        var sixMonthsAgo = now.AddMonths(-5);
        var cutoff      = new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Build 6 month label list — fixed boundary, not user-controlled
        const int trendMonths = 6;
        var monthSlots = new List<DateTime>(trendMonths);
        for (int i = 0; i < trendMonths; i++)
            monthSlots.Add(cutoff.AddMonths(i));
        var trendLabels = monthSlots.Select(m => m.ToString("MMM yyyy")).ToList();

        var trendEntries = await _db.KpiLogEntries
            .Where(e => e.LoggedAt >= cutoff)
            .Include(e => e.Kpi).ThenInclude(k => k.Department)
            .ToListAsync(ct);

        var trendDatasets = BuildTrendDatasets(trendEntries, monthSlots);

        // Active strategic goals
        var activeGoals = await _db.StrategicGoals
            .Where(g => g.Status != "Cancelled" && g.Status != "Completed")
            .Select(g => new StrategicGoalRowViewModel(
                g.Title,
                g.Perspective,
                g.Status,
                g.DueDate.HasValue ? g.DueDate.Value.ToString(DateFormatShort) : null))
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

        return View(ViewDashboard, new ManagerDashboardViewModel
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
            var latest = k.LogEntries.OrderByDescending(e => e.LoggedAt).ThenByDescending(e => e.Id).FirstOrDefault();
            var status = latest?.Status ?? StatusNoData;
            return new KpiRowViewModel(
                k.Name,
                k.Perspective,
                FormatValue(k.Target, k.Unit),
                latest != null ? FormatValue(latest.ActualValue, k.Unit) : "—",
                status);
        }).ToList();

        var myKpis  = myKpiList.Count;
        var onTrack = myKpiList.Count(k => k.Status == StatusOnTrack);
        var atRisk  = myKpiList.Count(k => k.Status == StatusAtRisk);
        var behind  = myKpiList.Count(k => k.Status == StatusBehind);

        // Pending KPIs: no entry in current month
        var deptKpiIds = deptKpis.Select(k => k.Id).ToList();
        var loggedKpiIdsThisMonth = await _db.KpiLogEntries
            .Where(e => e.LoggedAt >= currentMonth && deptKpiIds.Contains(e.KpiId))
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
                g.Count(k => k.Status == StatusOnTrack),
                g.Count(k => k.Status == StatusAtRisk),
                g.Count(k => k.Status == StatusBehind)))
            .ToList();

        var unreadNotifications = await _db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return View(ViewDashboard, new StaffDashboardViewModel
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
        var latestByKpi = await GetLatestEntriesPerKpiAsync(ct);

        var kpisWithEntries = latestByKpi.Count;
        var onTrackCount    = latestByKpi.Count(e => e.Status == StatusOnTrack);
        var atRiskCount     = latestByKpi.Count(e => e.Status == StatusAtRisk);
        var behindCount     = latestByKpi.Count(e => e.Status == StatusBehind);

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

        var bscPerspectives = BuildBscPerspectives(
            latestKpisWithPerspective.Select(k => (k.Id, k.Perspective)),
            latestByKpiDict);

        // Top departments by performance score
        var activeKpisWithDept = await _db.Kpis
            .Where(k => k.IsActive)
            .Include(k => k.Department)
            .ToListAsync(ct);

        var topDepartments = BuildTopDepartments(activeKpisWithDept, latestByKpiDict);

        // Underperforming KPIs (latest status == StatusBehind)
        var behindKpiIds = latestByKpi
            .Where(e => e.Status == StatusBehind)
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

        var underperformingList = BuildUnderperformingList(
            underperformingKpis.Select(k => (k.Id, k.Name, k.DeptName, k.Target, k.Unit)),
            latestByKpiDict);

        return View(ViewDashboard, new ExecutiveDashboardViewModel
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
        bool showArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Manager", "User", "Executive")) return Forbid();
        ViewData[VdTitle] = "KPI Tracking";
        ViewBag.ShowArchived = showArchived;

        var selectedDepartment  = string.IsNullOrWhiteSpace(department)  ? "All" : department.Trim();
        var selectedPerspective = string.IsNullOrWhiteSpace(perspective) ? "All" : perspective.Trim();
        var selectedStatus      = string.IsNullOrWhiteSpace(status)      ? "All" : status.Trim();

        var kpisQuery = _db.Kpis
            .Where(k => k.IsActive == !showArchived)
            .Include(k => k.Department)
            .Include(k => k.LogEntries)
            .AsNoTracking();

        if (selectedDepartment  != "All") kpisQuery = kpisQuery.Where(k => k.Department.Name == selectedDepartment);
        if (selectedPerspective != "All") kpisQuery = kpisQuery.Where(k => k.Perspective     == selectedPerspective);

        var kpis = await kpisQuery.ToListAsync(cancellationToken);

        var kpiItems = kpis
            .Select(k =>
            {
                var latest    = k.LogEntries.OrderByDescending(e => e.LoggedAt).ThenByDescending(e => e.Id).FirstOrDefault();
                var kpiStatus = latest?.Status ?? StatusNoData;
                AlertSeverity severity;
                if (kpiStatus == StatusBehind)
                    severity = AlertSeverity.Critical;
                else if (kpiStatus == StatusAtRisk)
                    severity = AlertSeverity.Warning;
                else
                    severity = AlertSeverity.Standard;

                return new KpiTrackingItemViewModel
                {
                    Id          = k.Id,
                    Name        = k.Name,
                    Department  = k.Department.Name,
                    Perspective = k.Perspective,
                    Target      = FormatValue(k.Target, k.Unit),
                    Actual      = latest != null ? FormatValue(latest.ActualValue, k.Unit) : "—",
                    Status      = kpiStatus,
                    IsArchived  = !k.IsActive,
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
    public async Task<IActionResult> KPILogEntry(int? kpiId, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Manager", "User")) return Forbid();
        ViewData[VdTitle] = "KPI Log Entry";
        ViewBag.Kpis = await GetKpiDetailsAsync(cancellationToken);
        return View(new KpiLogEntryViewModel
        {
            LoggedAt = DateTime.Today,
            KpiId    = kpiId ?? 0
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KPILogEntry(KpiLogEntryViewModel model, CancellationToken cancellationToken)
    {
        ViewData[VdTitle] = "KPI Log Entry";
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

        if (computedStatus != StatusOnTrack)
        {
            _db.Notifications.Add(new Notification
            {
                UserId    = userId,
                Title     = $"{kpi.Name} is {computedStatus}",
                Message   = $"Actual value {FormatValue(model.ActualValue, kpi.Unit)} is below the target of {FormatValue(kpi.Target, kpi.Unit)} for {model.Period}.",
                Severity  = computedStatus == StatusBehind ? "Critical" : "Warning",
                Icon      = computedStatus == StatusBehind ? "bi-x-circle" : "bi-exclamation-triangle",
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

        TempData[TdSuccessMessage] = $"Entry saved. <strong>{kpi.Name}</strong> is <strong>{computedStatus}</strong> for {model.Period}.";
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
        ViewData[VdTitle] = "Notifications";

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
                Severity = ToAlertSeverity(n.Severity)
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
        ViewData[VdTitle] = "KPI Management";

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
        ViewData[VdTitle] = "Create KPI";
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));
        return View(ViewKpiForm, await BuildKpiFormAsync(new KpiFormViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KpiCreate(KpiFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData[VdTitle] = "Create KPI";
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));

        if (!ModelState.IsValid)
            return View(ViewKpiForm, await BuildKpiFormAsync(model, cancellationToken));

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
        TempData[TdSuccessMessage] = $"KPI <strong>{kpi.Name}</strong> created successfully.";
        return RedirectToAction(nameof(KpiManagement));
    }

    [HttpGet]
    public async Task<IActionResult> KpiEdit(int id, CancellationToken cancellationToken)
    {
        ViewData[VdTitle] = "Edit KPI";
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));

        var kpi = await _db.Kpis.FindAsync(new object[] { id }, cancellationToken);
        if (kpi is null) return NotFound();

        var form = new KpiFormViewModel
        {
            Id = kpi.Id, Name = kpi.Name, DepartmentId = kpi.DepartmentId,
            Perspective = kpi.Perspective, Unit = kpi.Unit, Target = kpi.Target,
            Description = kpi.Description, IsActive = kpi.IsActive
        };
        return View(ViewKpiForm, await BuildKpiFormAsync(form, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KpiEdit(KpiFormViewModel model, CancellationToken cancellationToken)
    {
        ViewData[VdTitle] = "Edit KPI";
        if (!CanManageKpis()) return RedirectToAction(nameof(Dashboard));

        if (!ModelState.IsValid)
            return View(ViewKpiForm, await BuildKpiFormAsync(model, cancellationToken));

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
        TempData[TdSuccessMessage] = $"KPI <strong>{kpi.Name}</strong> updated successfully.";
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
        ViewData[VdTitle] = "Balanced Scorecards";

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
                    var latest = k.LogEntries.OrderByDescending(e => e.LoggedAt).ThenByDescending(e => e.Id).FirstOrDefault();
                    return new ScorecardKpiRowViewModel
                    {
                        Name   = k.Name,
                        Target = FormatValue(k.Target, k.Unit),
                        Actual = latest != null ? FormatValue(latest.ActualValue, k.Unit) : "—",
                        Status = latest?.Status ?? StatusNoData
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
        ViewData[VdTitle] = "Performance Analytics";

        var selectedDept   = string.IsNullOrWhiteSpace(department) ? "All" : department.Trim();
        // Clamp months to allowed values — prevents loop boundary injection (SonarQube S6680)
        var allowedMonths  = new[] { 3, 6, 12 };
        var selectedMonths = allowedMonths.Contains(months) ? months : 6;

        var allDepts = await _db.Departments.OrderBy(d => d.Name).Select(d => d.Name).ToListAsync(cancellationToken);

        // Date range
        var now    = DateTime.UtcNow;
        var cutoff = new DateTime(now.AddMonths(-(selectedMonths - 1)).Year,
                                  now.AddMonths(-(selectedMonths - 1)).Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var monthSlots  = Enumerable.Range(0, Math.Clamp(selectedMonths, 1, 12)).Select(i => cutoff.AddMonths(i)).ToList();
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
        var latestByKpi = await GetLatestEntriesPerKpiAsync(cancellationToken);

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
                var onTrack     = withEntries.Count(k => latestDict[k.Id].Status == StatusOnTrack);
                var score       = withEntries.Count == 0 ? 0 : (int)Math.Round((double)onTrack / withEntries.Count * 100);
                return (Name: g.Key, Score: score);
            })
            .OrderByDescending(d => d.Score)
            .ToList();

        // Doughnut
        var onTrackCount = latestByKpi.Count(e => e.Status == StatusOnTrack);
        var atRiskCount  = latestByKpi.Count(e => e.Status == StatusAtRisk);
        var behindCount  = latestByKpi.Count(e => e.Status == StatusBehind);

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

    public async Task<IActionResult> StrategicPlanning(
        bool showArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Manager", "Executive")) return Forbid();
        ViewData[VdTitle] = "Strategic Planning";
        ViewBag.ShowArchived = showArchived;

        var goals = await _db.StrategicGoals
            .Include(g => g.Owner)
            .Where(g => g.IsArchived == showArchived)
            .OrderBy(g => g.Status).ThenBy(g => g.Title)
            .Select(g => new StrategicGoalCardViewModel
            {
                Id          = g.Id,
                Title       = g.Title,
                Description = g.Description,
                Perspective = g.Perspective,
                Status      = g.Status,
                DueDate     = g.DueDate.HasValue ? g.DueDate.Value.ToString(DateFormatShort) : null,
                OwnerName   = g.Owner != null ? g.Owner.FullName : null,
                IsArchived  = g.IsArchived
            })
            .ToListAsync(cancellationToken);

        return View(new StrategicPlanningViewModel { Goals = goals });
    }

    [HttpGet]
    public IActionResult StrategicGoalCreate()
    {
        if (!HasAccess("Admin", "Manager")) return Forbid();
        ViewData[VdTitle] = "Add Strategic Goal";
        return View(ViewStrategicGoalForm, new StrategicGoalFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StrategicGoalCreate(StrategicGoalFormViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Manager")) return Forbid();
        ViewData[VdTitle] = "Add Strategic Goal";

        if (!ModelState.IsValid)
            return View(ViewStrategicGoalForm, model);

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

        TempData[TdSuccessMessage] = $"Goal <strong>{goal.Title}</strong> created successfully.";
        return RedirectToAction(nameof(StrategicPlanning));
    }

    [HttpGet]
    public async Task<IActionResult> StrategicGoalEdit(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Manager")) return Forbid();
        ViewData[VdTitle] = "Edit Strategic Goal";

        var goal = await _db.StrategicGoals.FindAsync(new object[] { id }, cancellationToken);
        if (goal is null) return NotFound();

        return View(ViewStrategicGoalForm, new StrategicGoalFormViewModel
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
        ViewData[VdTitle] = "Edit Strategic Goal";

        if (!ModelState.IsValid)
            return View(ViewStrategicGoalForm, model);

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

        TempData[TdSuccessMessage] = $"Goal <strong>{goal.Title}</strong> updated.";
        return RedirectToAction(nameof(StrategicPlanning));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StrategicGoalArchive(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Manager")) return Forbid();

        var goal = await _db.StrategicGoals.FindAsync(new object[] { id }, cancellationToken);
        if (goal is null) return NotFound();

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        goal.IsArchived = !goal.IsArchived;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = goal.IsArchived ? "Archived Strategic Goal" : "Unarchived Strategic Goal",
            EntityType = "StrategicGoal", EntityId = goal.Id,
            Details = goal.Title, OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData[TdSuccessMessage] = goal.IsArchived
            ? $"Goal <strong>{goal.Title}</strong> archived."
            : $"Goal <strong>{goal.Title}</strong> restored.";
        return RedirectToAction(nameof(StrategicPlanning), new { showArchived = goal.IsArchived });
    }

    public async Task<IActionResult> ExecutiveReporting(CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Manager", "Executive")) return Forbid();
        ViewData[VdTitle] = "Executive Reporting";

        // Build available periods from actual log entries
        var periods = await _db.KpiLogEntries
            .Select(e => e.Period)
            .Distinct()
            .OrderByDescending(p => p)
            .ToListAsync(cancellationToken);

        var selectedPeriod = periods.FirstOrDefault() ?? string.Empty;

        // Latest entry per KPI (for overall stats)
        var latestByKpi = await GetLatestEntriesPerKpiAsync(cancellationToken);

        var totalKpis = await _db.Kpis.CountAsync(k => k.IsActive, cancellationToken);
        var onTrack   = latestByKpi.Count(e => e.Status == StatusOnTrack);
        var atRisk    = latestByKpi.Count(e => e.Status == StatusAtRisk);
        var behind    = latestByKpi.Count(e => e.Status == StatusBehind);
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

        var execKpis = kpiRows.Select(k => BuildExecKpiRow(k, latestDict)).ToList();

        // Scorecard by perspective
        var kpisWithPerspective = await _db.Kpis
            .Where(k => k.IsActive)
            .Select(k => new { k.Id, k.Perspective })
            .ToListAsync(cancellationToken);

        var perspectiveOrder = new[] { "Financial", "Customer", "Internal Process", "Learning & Growth" };
        var scorecards = BuildExecScorecards(perspectiveOrder, kpisWithPerspective.Select(k => (k.Id, k.Perspective)), latestDict);

        // Strategic goals
        var goalRows = await _db.StrategicGoals
            .OrderBy(g => g.Status).ThenBy(g => g.Title)
            .Select(g => new ExecGoalRowViewModel
            {
                Title   = g.Title,
                Status  = g.Status,
                DueDate = g.DueDate.HasValue ? g.DueDate.Value.ToString(DateFormatShort) : null
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

    public async Task<IActionResult> Profile(CancellationToken cancellationToken = default)
    {
        ViewData[VdTitle] = "Profile";

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 0;
        var user = await _db.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null) return RedirectToAction(nameof(Login));

        var parts    = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}" : user.FullName[..Math.Min(2, user.FullName.Length)];

        return View(new ProfileViewModel
        {
            FullName       = user.FullName,
            Email          = user.Email,
            Role           = user.Role,
            Initials       = initials.ToUpperInvariant(),
            DepartmentName = user.Department?.Name,
            LastLoginAt    = user.LastLoginAt.HasValue ? user.LastLoginAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt") : "Never",
            NewFullName    = user.FullName,
            NewEmail       = user.Email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken cancellationToken)
    {
        ViewData[VdTitle] = "Profile";

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 0;
        var user = await _db.Users
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null) return RedirectToAction(nameof(Login));

        PopulateProfileDisplayFields(model, user);

        var changingPassword = !string.IsNullOrWhiteSpace(model.CurrentPassword);
        await ValidateProfileUpdateAsync(model, user, userId, changingPassword, cancellationToken);

        if (!ModelState.IsValid)
            return View(model);

        // Apply changes
        user.FullName = model.NewFullName.Trim();
        user.Email    = model.NewEmail.Trim();

        if (changingPassword && !string.IsNullOrWhiteSpace(model.NewPassword))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, workFactor: 11);

        // Update session if name/initials changed
        var newParts    = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var newInitials = newParts.Length >= 2 ? $"{newParts[0][0]}{newParts[^1][0]}" : user.FullName[..Math.Min(2, user.FullName.Length)];
        HttpContext.Session.SetString(SessionUserName,     user.FullName);
        HttpContext.Session.SetString(SessionUserInitials, newInitials.ToUpperInvariant());

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = userId,
            Action     = "Updated Profile",
            EntityType = EntityAppUser,
            EntityId   = userId,
            Details    = changingPassword ? "Profile and password updated." : "Profile updated.",
            OccurredAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        TempData[TdSuccessMessage] = "Profile updated successfully.";
        return RedirectToAction(nameof(Profile));
    }

    private static void PopulateProfileDisplayFields(ProfileViewModel model, AppUser user)
    {
        var parts    = user.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = parts.Length >= 2 ? $"{parts[0][0]}{parts[^1][0]}" : user.FullName[..Math.Min(2, user.FullName.Length)];
        model.FullName       = user.FullName;
        model.Email          = user.Email;
        model.Role           = user.Role;
        model.Initials       = initials.ToUpperInvariant();
        model.DepartmentName = user.Department?.Name;
        model.LastLoginAt    = user.LastLoginAt.HasValue
            ? user.LastLoginAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
            : "Never";
    }

    private async Task ValidateProfileUpdateAsync(
        ProfileViewModel model, AppUser user, int userId, bool changingPassword, CancellationToken ct)
    {
        if (changingPassword)
        {
            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");

            if (string.IsNullOrWhiteSpace(model.NewPassword))
                ModelState.AddModelError(nameof(model.NewPassword), "New password is required when changing password.");
        }

        if (model.NewEmail != user.Email)
        {
            var emailTaken = await _db.Users.AnyAsync(u => u.Email == model.NewEmail.Trim() && u.Id != userId, ct);
            if (emailTaken)
                ModelState.AddModelError(nameof(model.NewEmail), "This email address is already in use.");
        }
    }

    public IActionResult AccessDenied()
    {
        ViewData[VdTitle] = "Access Denied";
        return View();
    }

    public async Task<IActionResult> DepartmentManagement(
        bool showArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "Department Management";
        ViewBag.ShowArchived = showArchived;

        var departments = await _db.Departments
            .Where(d => d.IsArchived == showArchived)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentRowViewModel
            {
                Id          = d.Id,
                Name        = d.Name,
                Description = d.Description,
                UserCount   = d.Users.Count,
                KpiCount    = d.Kpis.Count(k => k.IsActive),
                CreatedAt   = d.CreatedAt.ToString(DateFormatShort),
                IsArchived  = d.IsArchived
            })
            .ToListAsync(cancellationToken);

        return View(new DepartmentManagementViewModel { Departments = departments });
    }

    [HttpGet]
    public async Task<IActionResult> DepartmentCreate()
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "Add Department";
        return View(ViewDepartmentForm, new DepartmentFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentCreate(DepartmentFormViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "Add Department";

        if (!ModelState.IsValid)
            return View(ViewDepartmentForm, model);

        var exists = await _db.Departments.AnyAsync(d => d.Name == model.Name.Trim(), cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Name), "A department with this name already exists.");
            return View(ViewDepartmentForm, model);
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
            UserId = userId, Action = "Created Department", EntityType = EntityDepartment,
            Details = dept.Name, OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData[TdSuccessMessage] = $"Department <strong>{dept.Name}</strong> created successfully.";
        return RedirectToAction(nameof(DepartmentManagement));
    }

    [HttpGet]
    public async Task<IActionResult> DepartmentEdit(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "Edit Department";

        var dept = await _db.Departments.FindAsync(new object[] { id }, cancellationToken);
        if (dept is null) return NotFound();

        return View(ViewDepartmentForm, new DepartmentFormViewModel
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
        ViewData[VdTitle] = "Edit Department";

        if (!ModelState.IsValid)
            return View(ViewDepartmentForm, model);

        var dept = await _db.Departments.FindAsync(new object[] { model.Id }, cancellationToken);
        if (dept is null) return NotFound();

        var duplicate = await _db.Departments
            .AnyAsync(d => d.Name == model.Name.Trim() && d.Id != model.Id, cancellationToken);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.Name), "A department with this name already exists.");
            return View(ViewDepartmentForm, model);
        }

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        dept.Name        = model.Name.Trim();
        dept.Description = model.Description?.Trim();

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "Updated Department", EntityType = EntityDepartment, EntityId = dept.Id,
            Details = dept.Name, OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData[TdSuccessMessage] = $"Department <strong>{dept.Name}</strong> updated successfully.";
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
            UserId = userId, Action = "Deleted Department", EntityType = EntityDepartment, EntityId = dept.Id,
            Details = dept.Name, OccurredAt = DateTime.UtcNow
        });
        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync(cancellationToken);

        TempData[TdSuccessMessage] = $"Department <strong>{dept.Name}</strong> deleted.";
        return RedirectToAction(nameof(DepartmentManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DepartmentArchive(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();

        var dept = await _db.Departments.FindAsync(new object[] { id }, cancellationToken);
        if (dept is null) return NotFound();

        var userId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        dept.IsArchived = !dept.IsArchived;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = dept.IsArchived ? "Archived Department" : "Unarchived Department",
            EntityType = EntityDepartment, EntityId = dept.Id,
            Details = dept.Name, OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData[TdSuccessMessage] = dept.IsArchived
            ? $"Department <strong>{dept.Name}</strong> archived."
            : $"Department <strong>{dept.Name}</strong> restored.";
        return RedirectToAction(nameof(DepartmentManagement), new { showArchived = dept.IsArchived });
    }

    // ── User Management ───────────────────────────────────────────────────────
    public async Task<IActionResult> UserManagement(CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "User Management";

        var users = await _db.Users
            .Include(u => u.Department)
            .OrderBy(u => u.FullName)
            .Select(u => new UserRowDetailViewModel
            {
                Id             = u.Id,
                FullName       = u.FullName,
                Email          = u.Email,
                Role           = u.Role,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                CreatedAt      = u.CreatedAt.ToString(DateFormatShort),
                IsActive       = u.IsActive,
                LastLoginAt    = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToLocalTime().ToString("MMM d, yyyy h:mm tt") : null
            })
            .ToListAsync(cancellationToken);

        return View(new UserManagementListViewModel { Users = users });
    }

    [HttpGet]
    public async Task<IActionResult> UserCreate(CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "Create User";
        return View(ViewUserForm, await BuildUserFormAsync(new UserFormViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UserCreate(UserFormViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "Create User";

        // Password required on create
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Password is required when creating a user.");

        // Prevent assigning Admin role through the UI
        if (model.Role == "Admin")
        {
            ModelState.AddModelError(nameof(model.Role), "The Admin role cannot be assigned through User Management.");
        }

        if (!ModelState.IsValid)
            return View(ViewUserForm, await BuildUserFormAsync(model, cancellationToken));

        var emailExists = await _db.Users.AnyAsync(u => u.Email == model.Email.Trim(), cancellationToken);
        if (emailExists)
        {
            ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            return View(ViewUserForm, await BuildUserFormAsync(model, cancellationToken));
        }

        var actorId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        var user = new AppUser
        {
            FullName     = model.FullName.Trim(),
            Email        = model.Email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password!, workFactor: 11),
            Role         = model.Role,
            DepartmentId = model.DepartmentId,
            IsActive     = model.IsActive,
            CreatedAt    = DateTime.UtcNow
        };
        _db.Users.Add(user);
        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = actorId,
            Action     = "Created User",
            EntityType = EntityAppUser,
            Details    = $"{user.FullName} ({user.Email}) — Role: {user.Role}",
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData[TdSuccessMessage] = $"User <strong>{user.FullName}</strong> created successfully.";
        return RedirectToAction(nameof(UserManagement));
    }

    [HttpGet]
    public async Task<IActionResult> UserEdit(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "Edit User";

        var user = await _db.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user is null) return NotFound();

        var form = new UserFormViewModel
        {
            Id           = user.Id,
            FullName     = user.FullName,
            Email        = user.Email,
            Role         = user.Role,
            DepartmentId = user.DepartmentId,
            IsActive     = user.IsActive
        };
        return View(ViewUserForm, await BuildUserFormAsync(form, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UserEdit(UserFormViewModel model, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();
        ViewData[VdTitle] = "Edit User";

        if (!ModelState.IsValid)
            return View(ViewUserForm, await BuildUserFormAsync(model, cancellationToken));

        var user = await _db.Users.FindAsync(new object[] { model.Id }, cancellationToken);
        if (user is null) return NotFound();

        var emailExists = await _db.Users
            .AnyAsync(u => u.Email == model.Email.Trim() && u.Id != model.Id, cancellationToken);
        if (emailExists)
        {
            ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            return View(ViewUserForm, await BuildUserFormAsync(model, cancellationToken));
        }

        // Prevent assigning Admin role through the UI
        if (model.Role == "Admin")
        {
            ModelState.AddModelError(nameof(model.Role), "The Admin role cannot be assigned through User Management.");
            return View(ViewUserForm, await BuildUserFormAsync(model, cancellationToken));
        }

        var actorId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        user.FullName     = model.FullName.Trim();
        user.Email        = model.Email.Trim();
        user.Role         = model.Role;
        user.DepartmentId = model.DepartmentId;
        user.IsActive     = model.IsActive;

        if (!string.IsNullOrWhiteSpace(model.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, workFactor: 11);

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = actorId,
            Action     = "Updated User",
            EntityType = EntityAppUser,
            EntityId   = user.Id,
            Details    = $"{user.FullName} ({user.Email}) — Role: {user.Role}",
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData[TdSuccessMessage] = $"User <strong>{user.FullName}</strong> updated successfully.";
        return RedirectToAction(nameof(UserManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UserToggleActive(int id, CancellationToken cancellationToken)
    {
        if (!HasAccess("Admin", "Administrator")) return Forbid();

        var user = await _db.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user is null) return NotFound();

        var actorId = HttpContext.Session.GetInt32(SessionUserId) ?? 1;
        user.IsActive = !user.IsActive;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = actorId,
            Action     = user.IsActive ? "Activated User" : "Deactivated User",
            EntityType = EntityAppUser,
            EntityId   = user.Id,
            Details    = $"{user.FullName} set to {(user.IsActive ? "Active" : "Inactive")}.",
            OccurredAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData[TdSuccessMessage] = user.IsActive
            ? $"User <strong>{user.FullName}</strong> activated."
            : $"User <strong>{user.FullName}</strong> deactivated.";
        return RedirectToAction(nameof(UserManagement));
    }

    public async Task<IActionResult> AuditLog(CancellationToken cancellationToken = default)
    {
        if (!HasAccess("Admin")) return Forbid();
        ViewData[VdTitle] = "Audit Log";

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

    private async Task<UserFormViewModel> BuildUserFormAsync(UserFormViewModel model, CancellationToken ct)
    {
        model.Departments = await _db.Departments
            .Where(d => !d.IsArchived)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentOptionViewModel { Id = d.Id, Name = d.Name })
            .ToListAsync(ct);
        return model;
    }

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
        if (IsLowerBetterKpi(kpi))
        {
            if (actual <= kpi.Target)         return StatusOnTrack;
            if (actual <= kpi.Target * 1.25m) return StatusAtRisk;
            return StatusBehind;
        }

        if (actual >= kpi.Target)             return StatusOnTrack;
        if (actual >= kpi.Target * 0.85m)     return StatusAtRisk;
        return StatusBehind;
    }

    private static bool IsLowerBetterKpi(Kpi kpi) =>
        kpi.Unit == "days"
        || kpi.Name.Contains("Turnover", StringComparison.OrdinalIgnoreCase)
        || kpi.Name.Contains("Defect",   StringComparison.OrdinalIgnoreCase)
        || kpi.Name.Contains("Cycle",    StringComparison.OrdinalIgnoreCase);

    // ── Private query helpers ─────────────────────────────────────────────────

    /// <summary>Returns the single most-recent log entry for each KPI.</summary>
    private async Task<List<KpiLogEntry>> GetLatestEntriesPerKpiAsync(CancellationToken ct) =>
        await _db.KpiLogEntries
            .GroupBy(e => e.KpiId)
            .Select(g => g.OrderByDescending(e => e.LoggedAt).ThenByDescending(e => e.Id).First())
            .ToListAsync(ct);

    /// <summary>Returns a department overview (name, user count, active KPI count) for every department.</summary>
    private async Task<List<DepartmentOverviewViewModel>> GetDepartmentOverviewAsync(CancellationToken ct) =>
        await _db.Departments
            .Select(d => new DepartmentOverviewViewModel(
                d.Name,
                d.Users.Count,
                d.Kpis.Count(k => k.IsActive)))
            .ToListAsync(ct);

    /// <summary>Returns the distinct KPI IDs that have at least one log entry in the current calendar month.</summary>
    private async Task<List<int>> GetLoggedKpiIdsThisMonthAsync(CancellationToken ct)
    {
        var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await _db.KpiLogEntries
            .Where(e => e.LoggedAt >= currentMonth)
            .Select(e => e.KpiId)
            .Distinct()
            .ToListAsync(ct);
    }

    // ── Private static computation helpers ───────────────────────────────────

    /// <summary>
    /// Builds one <see cref="TrendDatasetViewModel"/> per department from the supplied log entries,
    /// filling <c>null</c> for any month slot that has no data.
    /// </summary>
    private static List<TrendDatasetViewModel> BuildTrendDatasets(
        List<KpiLogEntry> entries,
        List<DateTime> monthSlots)
    {
        // Group by (DepartmentName, Year, Month) → average ActualValue
        var trendGrouped = entries
            .GroupBy(e => (DeptName: e.Kpi.Department.Name, Year: e.LoggedAt.Year, Month: e.LoggedAt.Month))
            .ToDictionary(g => g.Key, g => g.Average(e => e.ActualValue));

        var deptNames = entries.Select(e => e.Kpi.Department.Name).Distinct().OrderBy(n => n).ToList();

        return deptNames.Select(dept =>
        {
            var values = monthSlots.Select(slot =>
            {
                var key = (DeptName: dept, Year: slot.Year, Month: slot.Month);
                return trendGrouped.TryGetValue(key, out var avg) ? (decimal?)avg : null;
            }).ToList();
            return new TrendDatasetViewModel(dept, values);
        }).ToList();
    }

    /// <summary>
    /// Calculates a performance score (% On Track) for each department and returns them
    /// ordered descending by score.
    /// </summary>
    private static List<DeptPerformanceViewModel> BuildTopDepartments(
        List<Kpi> kpisWithDept,
        Dictionary<int, KpiLogEntry> latestDict) =>
        kpisWithDept
            .GroupBy(k => k.Department.Name)
            .Select(g =>
            {
                var deptKpisWithEntries = g.Where(k => latestDict.ContainsKey(k.Id)).ToList();
                var deptOnTrack = deptKpisWithEntries.Count(k => latestDict[k.Id].Status == StatusOnTrack);
                var scorePct = deptKpisWithEntries.Count == 0
                    ? 0
                    : (int)Math.Round((double)deptOnTrack / deptKpisWithEntries.Count * 100);
                return new DeptPerformanceViewModel(g.Key, scorePct);
            })
            .OrderByDescending(d => d.ScorePct)
            .ToList();

    /// <summary>
    /// Maps a list of underperforming KPI projections to <see cref="UnderperformingKpiViewModel"/>
    /// by looking up the latest actual value from <paramref name="latestDict"/>.
    /// </summary>
    private static List<UnderperformingKpiViewModel> BuildUnderperformingList(
        IEnumerable<(int Id, string Name, string DeptName, decimal Target, string Unit)> kpis,
        Dictionary<int, KpiLogEntry> latestDict) =>
        kpis.Select(k =>
        {
            var latest = latestDict[k.Id];
            return new UnderperformingKpiViewModel(
                k.Name,
                k.DeptName,
                FormatValue(k.Target, k.Unit),
                FormatValue(latest.ActualValue, k.Unit));
        }).ToList();

    /// <summary>
    /// Groups KPI perspective projections by perspective and counts On Track / At Risk / Behind
    /// using the supplied latest-entry dictionary.
    /// </summary>
    private static List<BscPerspectiveViewModel> BuildBscPerspectives(
        IEnumerable<(int Id, string Perspective)> kpisWithPerspective,
        Dictionary<int, KpiLogEntry> latestDict) =>
        kpisWithPerspective
            .GroupBy(k => k.Perspective)
            .Select(g => new BscPerspectiveViewModel(
                g.Key,
                g.Count(k => latestDict.TryGetValue(k.Id, out var e) && e.Status == StatusOnTrack),
                g.Count(k => latestDict.TryGetValue(k.Id, out var e) && e.Status == StatusAtRisk),
                g.Count(k => latestDict.TryGetValue(k.Id, out var e) && e.Status == StatusBehind)))
            .ToList();

    private static AlertSeverity ToAlertSeverity(string? severity) =>
        severity == "Critical" ? AlertSeverity.Critical
        : severity == "Warning" ? AlertSeverity.Warning
        : AlertSeverity.Standard;

    private ExecKpiRowViewModel BuildExecKpiRow(Kpi k, Dictionary<int, KpiLogEntry> latestDict)
    {
        latestDict.TryGetValue(k.Id, out var latest);
        var actualVal   = latest != null ? latest.ActualValue : (decimal?)null;
        var statusStr   = latest?.Status ?? StatusNoData;
        var isWholeUnit = k.Unit.Equals("days",  StringComparison.OrdinalIgnoreCase)
                       || k.Unit.Equals("hrs",   StringComparison.OrdinalIgnoreCase)
                       || k.Unit.Equals("count", StringComparison.OrdinalIgnoreCase);

        string variance;
        if (!actualVal.HasValue)
        {
            variance = "—";
        }
        else
        {
            var sign    = actualVal.Value >= k.Target ? "+" : "";
            var diff    = actualVal.Value - k.Target;
            var diffStr = isWholeUnit ? $"{diff:F0}" : $"{diff:F2}";
            variance    = $"{sign}{diffStr} {k.Unit}";
        }

        return new ExecKpiRowViewModel
        {
            Name       = k.Name,
            Department = k.Department.Name,
            Target     = FormatValue(k.Target, k.Unit),
            Actual     = actualVal.HasValue ? FormatValue(actualVal.Value, k.Unit) : "—",
            Status     = statusStr,
            Variance   = variance
        };
    }

    private static List<ExecScorecardRowViewModel> BuildExecScorecards(
        IEnumerable<string> perspectiveOrder,
        IEnumerable<(int Id, string Perspective)> kpisWithPerspective,
        Dictionary<int, KpiLogEntry> latestDict)
    {
        var kpiList = kpisWithPerspective.ToList();
        return perspectiveOrder.Select(p =>
        {
            var inPerspective = kpiList.Where(k => k.Perspective == p).ToList();
            var withEntries   = inPerspective.Where(k => latestDict.ContainsKey(k.Id)).ToList();
            return new ExecScorecardRowViewModel
            {
                Perspective = p,
                OnTrack     = withEntries.Count(k => latestDict[k.Id].Status == StatusOnTrack),
                AtRisk      = withEntries.Count(k => latestDict[k.Id].Status == StatusAtRisk),
                Behind      = withEntries.Count(k => latestDict[k.Id].Status == StatusBehind),
                Total       = inPerspective.Count
            };
        }).Where(s => s.Total > 0).ToList();
    }

    private static string ToRelativeTime(DateTime utc)    {
        var diff = DateTime.UtcNow - utc;
        if (diff.TotalMinutes < 1)  return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays    < 7)  return $"{(int)diff.TotalDays}d ago";
        return utc.ToLocalTime().ToString(DateFormatShort);
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
