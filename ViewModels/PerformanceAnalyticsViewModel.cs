namespace PeakMetrics.Web.ViewModels;

public sealed class PerformanceAnalyticsViewModel
{
    // Filter state
    public string SelectedDepartment { get; init; } = "All";
    public int    SelectedMonths     { get; init; } = 6;
    public IReadOnlyList<string> Departments { get; init; } = Array.Empty<string>();

    // Line chart: one dataset per perspective, monthly averages
    public IReadOnlyList<string>                TrendLabels   { get; init; } = Array.Empty<string>();
    public IReadOnlyList<TrendDatasetViewModel> TrendDatasets { get; init; } = Array.Empty<TrendDatasetViewModel>();

    // Bar chart: department performance scores
    public IReadOnlyList<string> BarLabels  { get; init; } = Array.Empty<string>();
    public IReadOnlyList<int>    BarScores  { get; init; } = Array.Empty<int>();

    // Doughnut: On Track / At Risk / Behind
    public int DoughnutOnTrack { get; init; }
    public int DoughnutAtRisk  { get; init; }
    public int DoughnutBehind  { get; init; }
}
