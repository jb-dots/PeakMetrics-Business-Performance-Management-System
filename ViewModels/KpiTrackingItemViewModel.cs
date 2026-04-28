namespace PeakMetrics.Web.ViewModels;

public sealed class KpiTrackingItemViewModel
{
    public int    Id         { get; init; }
    public string Name       { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Perspective{ get; init; } = string.Empty;
    public string Target     { get; init; } = string.Empty;
    public string Actual     { get; init; } = string.Empty;
    public string Status     { get; init; } = string.Empty;
    public bool   IsArchived { get; init; }
    public AlertSeverity Severity { get; init; } = AlertSeverity.Standard;
}
