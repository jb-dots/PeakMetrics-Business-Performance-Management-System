namespace PeakMetrics.Web.ViewModels;

public sealed record AuditLogRowViewModel(string UserName, string Action, string EntityType, string RelativeTime);
public sealed record DepartmentOverviewViewModel(string Name, int UserCount, int ActiveKpiCount);
public sealed record UserRowViewModel(string FullName, string Role, string DepartmentName, string CreatedAt);
public sealed record DeptKpiStatusViewModel(string Department, int OnTrack, int AtRisk, int Behind);
public sealed record TrendDatasetViewModel(string DepartmentName, IReadOnlyList<decimal?> Values);
public sealed record StrategicGoalRowViewModel(string Title, string Perspective, string Status, int? TargetYear);
public sealed record RecentKpiLogViewModel(string KpiName, string LoggedBy, string ActualWithUnit, string Status, string RelativeTime);
public sealed record KpiRowViewModel(string Name, string Perspective, string Target, string Actual, string Status);
public sealed record ScorecardPerspectiveViewModel(string Perspective, int OnTrack, int AtRisk, int Behind);
public sealed record BscPerspectiveViewModel(string Perspective, int OnTrack, int AtRisk, int Behind);
public sealed record DeptPerformanceViewModel(string DepartmentName, int ScorePct);
public sealed record UnderperformingKpiViewModel(string KpiName, string DepartmentName, string Target, string Actual);
