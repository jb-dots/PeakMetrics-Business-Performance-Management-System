namespace PeakMetrics.Web.ViewModels;

public sealed class KpiTrackingPageViewModel
{
    public IReadOnlyList<KpiTrackingItemViewModel> Kpis { get; init; } = Array.Empty<KpiTrackingItemViewModel>();

    public IReadOnlyList<string> Departments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Perspectives { get; init; } = Array.Empty<string>();

    public string SelectedDepartment { get; init; } = "All";

    public string SelectedPerspective { get; init; } = "All";

    public string SelectedStatus { get; init; } = "All";

    public bool IsFallback { get; init; }

    public string DataMessage { get; init; } = string.Empty;
}
