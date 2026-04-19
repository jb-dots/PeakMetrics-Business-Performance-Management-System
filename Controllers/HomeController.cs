using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using PeakMetrics.Web.Services;
using PeakMetrics.Web.ViewModels;

namespace PeakMetrics.Web.Controllers;

public class HomeController : Controller
{
    private readonly IFreeApiDataService _freeApiDataService;

    public HomeController(IFreeApiDataService freeApiDataService)
    {
        _freeApiDataService = freeApiDataService;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionName = context.RouteData.Values["action"]?.ToString() ?? string.Empty;
        if (!string.Equals(actionName, nameof(Login), StringComparison.OrdinalIgnoreCase))
        {
            var notificationsResult = await _freeApiDataService.GetNotificationsAsync(cancellationToken: context.HttpContext.RequestAborted);
            var notifications = notificationsResult.Data;
            ViewData["QuickNotifications"] = notifications.Take(5).ToList();
            ViewData["UnreadQuickNotifications"] = notifications.Count(n => !n.Read);
        }

        await next();
    }

    public IActionResult Index() => RedirectToAction(nameof(Login));

    public IActionResult Login()
    {
        ViewData["Title"] = "Login";
        return View();
    }

    public async Task<IActionResult> Dashboard(bool refresh = false, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Dashboard";
        var result = await _freeApiDataService.GetRecentActivitiesAsync(refresh, cancellationToken);

        var model = new DashboardPageViewModel
        {
            Activities = result.Data,
            IsFallback = result.IsFallback,
            DataMessage = result.Message
        };

        return View(model);
    }

    public async Task<IActionResult> KPITracking(
        string? department,
        string? perspective,
        string? status,
        bool refresh = false,
        CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "KPI Tracking";

        var result = await _freeApiDataService.GetKpisAsync(refresh, cancellationToken);
        var allKpis = result.Data;

        var selectedDepartment = string.IsNullOrWhiteSpace(department) ? "All" : department.Trim();
        var selectedPerspective = string.IsNullOrWhiteSpace(perspective) ? "All" : perspective.Trim();
        var selectedStatus = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim();

        var filtered = allKpis.Where(kpi =>
            (selectedDepartment == "All" || kpi.Department == selectedDepartment) &&
            (selectedPerspective == "All" || kpi.Perspective == selectedPerspective) &&
            (selectedStatus == "All" || kpi.Status == selectedStatus))
            .ToList();

        var model = new KpiTrackingPageViewModel
        {
            Kpis = filtered,
            Departments = allKpis.Select(kpi => kpi.Department).Distinct().OrderBy(x => x).ToList(),
            Perspectives = allKpis.Select(kpi => kpi.Perspective).Distinct().OrderBy(x => x).ToList(),
            SelectedDepartment = selectedDepartment,
            SelectedPerspective = selectedPerspective,
            SelectedStatus = selectedStatus,
            IsFallback = result.IsFallback,
            DataMessage = result.Message
        };

        return View(model);
    }

    public IActionResult KPILogEntry()
    {
        ViewData["Title"] = "KPI Log Entry";
        return View();
    }

    public IActionResult BalancedScorecard()
    {
        ViewData["Title"] = "Balanced Scorecards";
        return View();
    }

    public IActionResult PerformanceAnalytics()
    {
        ViewData["Title"] = "Performance Analytics";
        return View();
    }

    public IActionResult StrategicPlanning()
    {
        ViewData["Title"] = "Strategic Planning";
        return View();
    }

    public IActionResult ExecutiveReporting()
    {
        ViewData["Title"] = "Executive Reporting";
        return View();
    }

    public async Task<IActionResult> Notifications(bool refresh = false, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Notifications";

        var result = await _freeApiDataService.GetNotificationsAsync(refresh, cancellationToken);
        var model = new NotificationsPageViewModel
        {
            Notifications = result.Data,
            IsFallback = result.IsFallback,
            DataMessage = result.Message
        };

        return View(model);
    }

    public IActionResult Profile()
    {
        ViewData["Title"] = "Profile";
        return View();
    }

    public IActionResult DepartmentManagement()
    {
        ViewData["Title"] = "Department Management";
        return View();
    }

    public IActionResult AuditLog()
    {
        ViewData["Title"] = "Audit Log";
        return View();
    }
}
