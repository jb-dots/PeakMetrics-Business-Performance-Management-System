namespace PeakMetrics.Web.ViewModels;

public sealed class DashboardPageViewModel
{
    public IReadOnlyList<ActivityItemViewModel> Activities { get; init; } = Array.Empty<ActivityItemViewModel>();

    public bool IsFallback { get; init; }

    public string DataMessage { get; init; } = string.Empty;
}
