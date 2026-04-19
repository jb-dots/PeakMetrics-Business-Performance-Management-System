namespace PeakMetrics.Web.ViewModels;

public sealed class NotificationItemViewModel
{
    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Time { get; init; } = string.Empty;

    public bool Read { get; init; }

    public string Icon { get; init; } = "bi-info-circle";

    public AlertSeverity Severity { get; init; } = AlertSeverity.Standard;
}
