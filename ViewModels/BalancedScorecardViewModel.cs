namespace PeakMetrics.Web.ViewModels;

public sealed class BalancedScorecardViewModel
{
    public IReadOnlyList<ScorecardPerspectiveGroupViewModel> Perspectives { get; init; } = Array.Empty<ScorecardPerspectiveGroupViewModel>();
}

public sealed class ScorecardPerspectiveGroupViewModel
{
    public string Perspective { get; init; } = string.Empty;
    public IReadOnlyList<ScorecardKpiRowViewModel> Kpis { get; init; } = Array.Empty<ScorecardKpiRowViewModel>();
}

public sealed class ScorecardKpiRowViewModel
{
    public string Name   { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string Actual { get; init; } = string.Empty;
    public string Status { get; init; } = "No Data";
}
