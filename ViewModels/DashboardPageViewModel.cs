namespace PeakMetrics.Web.ViewModels;

public sealed class DashboardPageViewModel
{
    public IReadOnlyList<ActivityItemViewModel> Activities { get; init; } = Array.Empty<ActivityItemViewModel>();

    public int TotalKpis { get; init; }
    public int OnTrack   { get; init; }
    public int AtRisk    { get; init; }
    public int Behind    { get; init; }

    public bool IsFallback { get; init; }
    public string DataMessage { get; init; } = string.Empty;
}
