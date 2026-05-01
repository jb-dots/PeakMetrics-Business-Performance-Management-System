namespace PeakMetrics.Web.ViewModels;

public sealed class ExecutiveReportingViewModel
{
    public int TotalKpis  { get; init; }
    public int OnTrack    { get; init; }
    public int AtRisk     { get; init; }
    public int Behind     { get; init; }
    public int NoData     { get; init; }
    public int OverallPct { get; init; }

    public IReadOnlyList<ExecKpiRowViewModel>          Kpis         { get; init; } = Array.Empty<ExecKpiRowViewModel>();
    public IReadOnlyList<ExecScorecardRowViewModel>    Scorecards   { get; init; } = Array.Empty<ExecScorecardRowViewModel>();
    public IReadOnlyList<ExecGoalRowViewModel>         Goals        { get; init; } = Array.Empty<ExecGoalRowViewModel>();

    public string SelectedPeriod { get; init; } = string.Empty;
    public IReadOnlyList<string> AvailablePeriods { get; init; } = Array.Empty<string>();
}

public sealed class ExecKpiRowViewModel
{
    public string Name       { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Target     { get; init; } = string.Empty;
    public string Actual     { get; init; } = string.Empty;
    public string Status     { get; init; } = string.Empty;
    public string Variance   { get; init; } = string.Empty;
}

public sealed class ExecScorecardRowViewModel
{
    public string Perspective { get; init; } = string.Empty;
    public int    OnTrack     { get; init; }
    public int    AtRisk      { get; init; }
    public int    Behind      { get; init; }
    public int    Total       { get; init; }
}

public sealed class ExecGoalRowViewModel
{
    public string  Title      { get; init; } = string.Empty;
    public string  Status     { get; init; } = string.Empty;
    public int?    TargetYear { get; init; }
}
