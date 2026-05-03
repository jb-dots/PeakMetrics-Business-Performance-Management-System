namespace PeakMetrics.Web.ViewModels;

public sealed class SuperAdminDashboardViewModel
{
    public int TotalUsers        { get; init; }
    public int TotalDepartments  { get; init; }
    public int TotalKpis         { get; init; }
    public int TotalAuditEntries { get; init; }

    // System Health Snapshot
    public int TotalNotifications  { get; init; }
    public int TotalKpiLogEntries  { get; init; }

    // Recent Audit Log (top 10)
    public IReadOnlyList<AuditLogRowViewModel> RecentAuditLogs { get; init; } = Array.Empty<AuditLogRowViewModel>();

    // Role distribution chart: key = role string, value = count
    public IReadOnlyDictionary<string, int> RoleDistribution { get; init; } = new Dictionary<string, int>();

    // Department overview table
    public IReadOnlyList<DepartmentOverviewViewModel> Departments { get; init; } = Array.Empty<DepartmentOverviewViewModel>();
}

public sealed class AdministratorDashboardViewModel
{
    public int TotalUsers          { get; init; }
    public int TotalDepartments    { get; init; }
    public int PendingKpis         { get; init; }
    public int NewUsersThisMonth   { get; init; }
    public int UnreadNotifications { get; init; }

    // New summary cards
    public int TotalKpis           { get; init; }
    public int ActiveRoles         { get; init; }

    // KPI status overview (read-only counts from latest log entries)
    public int KpiOnTrack          { get; init; }
    public int KpiAtRisk           { get; init; }
    public int KpiBehind           { get; init; }

    // Role distribution chart: key = role string, value = count
    public IReadOnlyDictionary<string, int> RoleDistribution { get; init; } = new Dictionary<string, int>();

    // User list overview (top 5 most recently created)
    public IReadOnlyList<UserRowViewModel> RecentUsers { get; init; } = Array.Empty<UserRowViewModel>();

    // Department summary table
    public IReadOnlyList<DepartmentOverviewViewModel> Departments { get; init; } = Array.Empty<DepartmentOverviewViewModel>();

    // System logs: recent movements of non-admin users (top 10)
    public IReadOnlyList<AuditLogRowViewModel> RecentSystemLogs { get; init; } = Array.Empty<AuditLogRowViewModel>();
}

public sealed class ManagerDashboardViewModel
{
    public int TotalKpis           { get; init; }
    public int OnTrack             { get; init; }
    public int AtRisk              { get; init; }
    public int Behind              { get; init; }
    public int UnreadNotifications { get; init; }

    // Bar chart: department name → status counts
    public IReadOnlyList<DeptKpiStatusViewModel> DeptKpiStatuses { get; init; } = Array.Empty<DeptKpiStatusViewModel>();

    // Trend line chart: one dataset per department, 6 months
    public IReadOnlyList<string>                TrendLabels   { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TrendDatasetViewModel> TrendDatasets { get; init; } = Array.Empty<TrendDatasetViewModel>();

    // Strategic goals (active only)
    public IReadOnlyList<StrategicGoalRowViewModel> ActiveGoals { get; init; } = Array.Empty<StrategicGoalRowViewModel>();

    // Recent KPI log entries (top 5)
    public IReadOnlyList<RecentKpiLogViewModel> RecentLogs { get; init; } = Array.Empty<RecentKpiLogViewModel>();
}

public sealed class StaffDashboardViewModel
{
    public int  MyKpis             { get; init; }
    public int  OnTrack            { get; init; }
    public int  AtRisk             { get; init; }
    public int  Behind             { get; init; }
    public int  UnreadNotifications { get; init; }
    public bool HasDepartment      { get; init; }

    // My KPI list (all active KPIs in user's department)
    public IReadOnlyList<KpiRowViewModel> MyKpiList { get; init; } = Array.Empty<KpiRowViewModel>();

    // Due for logging (Pending KPIs in user's department)
    public IReadOnlyList<string> PendingKpiNames { get; init; } = Array.Empty<string>();

    // My recent logs (top 5 by current user)
    public IReadOnlyList<RecentKpiLogViewModel> MyRecentLogs { get; init; } = Array.Empty<RecentKpiLogViewModel>();

    // My scorecard: perspective → status counts
    public IReadOnlyList<ScorecardPerspectiveViewModel> Scorecard { get; init; } = Array.Empty<ScorecardPerspectiveViewModel>();
}

public sealed class ExecutiveDashboardViewModel
{
    public int OverallPerformancePct { get; init; }
    public int TotalKpis             { get; init; }
    public int ActiveGoals           { get; init; }
    public int DepartmentsTracked    { get; init; }

    // BSC perspective cards (4 items)
    public IReadOnlyList<BscPerspectiveViewModel> BscPerspectives { get; init; } = Array.Empty<BscPerspectiveViewModel>();

    // Doughnut chart: On Track / At Risk / Behind counts
    public int ChartOnTrack { get; init; }
    public int ChartAtRisk  { get; init; }
    public int ChartBehind  { get; init; }

    // Top performing departments (all, ranked descending by score)
    public IReadOnlyList<DeptPerformanceViewModel> TopDepartments { get; init; } = Array.Empty<DeptPerformanceViewModel>();

    // Underperforming KPIs (latest status == "Behind")
    public IReadOnlyList<UnderperformingKpiViewModel> UnderperformingKpis { get; init; } = Array.Empty<UnderperformingKpiViewModel>();
}
