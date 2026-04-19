using PeakMetrics.Web.ViewModels;

namespace PeakMetrics.Web.Services;

public interface IFreeApiDataService
{
    Task<ApiDataResult<IReadOnlyList<NotificationItemViewModel>>> GetNotificationsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    Task<ApiDataResult<IReadOnlyList<ActivityItemViewModel>>> GetRecentActivitiesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);

    Task<ApiDataResult<IReadOnlyList<KpiTrackingItemViewModel>>> GetKpisAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}
