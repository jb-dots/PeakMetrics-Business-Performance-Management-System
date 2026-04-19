namespace PeakMetrics.Web.ViewModels;

public sealed class NotificationsPageViewModel
{
    public IReadOnlyList<NotificationItemViewModel> Notifications { get; init; } = Array.Empty<NotificationItemViewModel>();

    public bool IsFallback { get; init; }

    public string DataMessage { get; init; } = string.Empty;
}
