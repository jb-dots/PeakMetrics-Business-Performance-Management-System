using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using PeakMetrics.Web.ViewModels;

namespace PeakMetrics.Web.Services;

public sealed class FreeApiDataService : IFreeApiDataService
{
    private const string UsersCacheKey = "free-api-users";
    private const string TodosCacheKey = "free-api-todos";
    private static readonly string[] Departments = ["Finance", "HR", "Sales", "Operations", "Customer Service", "Quality"];
    private static readonly string[] Perspectives = ["Financial", "Customer", "Internal Process", "Learning & Growth"];

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public FreeApiDataService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<ApiDataResult<IReadOnlyList<NotificationItemViewModel>>> GetNotificationsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var todos = await GetTodosAsync(forceRefresh, cancellationToken);
        if (todos.Count == 0)
        {
            return new ApiDataResult<IReadOnlyList<NotificationItemViewModel>>
            {
                Data = GetFallbackNotifications(),
                IsFallback = true,
                Message = "Live notifications unavailable. Showing fallback feed."
            };
        }

        var data = todos
            .Take(8)
            .Select((todo, index) => new NotificationItemViewModel
            {
                Title = ToTitle(todo.Title),
                Message = todo.Completed
                    ? "Task from the external feed is completed and archived."
                    : "Task from the external feed requires follow-up this cycle.",
                Time = ToRelativeTime(index),
                Read = todo.Completed,
                Severity = AlertSeverityRules.FromNotificationText(todo.Title, todo.Completed ? "completed" : "needs attention"),
                Icon = todo.Completed ? "bi-check-circle" : "bi-exclamation-triangle"
            })
            .ToList();

        return new ApiDataResult<IReadOnlyList<NotificationItemViewModel>>
        {
            Data = data,
            IsFallback = false,
            Message = "Live notifications synced from JSONPlaceholder."
        };
    }

    public async Task<ApiDataResult<IReadOnlyList<ActivityItemViewModel>>> GetRecentActivitiesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var todos = await GetTodosAsync(forceRefresh, cancellationToken);
        var users = await GetUsersAsync(forceRefresh, cancellationToken);

        if (todos.Count == 0)
        {
            return new ApiDataResult<IReadOnlyList<ActivityItemViewModel>>
            {
                Data = GetFallbackActivities(),
                IsFallback = true,
                Message = "Live activity feed unavailable. Showing fallback feed."
            };
        }

        var userLookup = users.ToDictionary(user => user.Id, user => user.Name);

        var data = todos
            .Take(10)
            .Select((todo, index) => new ActivityItemViewModel
            {
                User = userLookup.TryGetValue(todo.UserId, out var name) ? name : "PeakMetrics User",
                Action = todo.Completed ? "Updated KPI" : "Needs Review",
                Item = ToTitle(todo.Title),
                Time = ToRelativeTime(index)
            })
            .ToList();

        return new ApiDataResult<IReadOnlyList<ActivityItemViewModel>>
        {
            Data = data,
            IsFallback = false,
            Message = "Live activity feed synced from JSONPlaceholder."
        };
    }

    public async Task<ApiDataResult<IReadOnlyList<KpiTrackingItemViewModel>>> GetKpisAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var todos = await GetTodosAsync(forceRefresh, cancellationToken);
        if (todos.Count == 0)
        {
            return new ApiDataResult<IReadOnlyList<KpiTrackingItemViewModel>>
            {
                Data = GetFallbackKpis(),
                IsFallback = true,
                Message = "Live KPI feed unavailable. Showing fallback KPI data."
            };
        }

        var kpis = todos
            .Take(20)
            .Select((todo, index) =>
            {
                var department = Departments[todo.UserId % Departments.Length];
                var perspective = Perspectives[todo.UserId % Perspectives.Length];
                var status = todo.Completed ? "On Track" : index % 2 == 0 ? "At Risk" : "Behind";
                var target = GetTargetValue(perspective);
                var actual = GetActualValue(perspective, status, index);

                return new KpiTrackingItemViewModel
                {
                    Name = ToTitle(todo.Title),
                    Department = department,
                    Perspective = perspective,
                    Target = target,
                    Actual = actual,
                    Status = status,
                    Severity = AlertSeverityRules.FromKpiStatus(status)
                };
            })
            .ToList();

        return new ApiDataResult<IReadOnlyList<KpiTrackingItemViewModel>>
        {
            Data = kpis,
            IsFallback = false,
            Message = "Live KPI feed synced from JSONPlaceholder."
        };
    }

    private async Task<IReadOnlyList<JsonTodo>> GetTodosAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _cache.TryGetValue(TodosCacheKey, out IReadOnlyList<JsonTodo>? cachedTodos) && cachedTodos is not null)
        {
            return cachedTodos;
        }

        var todos = await GetWithRetryAsync<List<JsonTodo>>(
            () => _httpClient.GetFromJsonAsync<List<JsonTodo>>("/todos?_limit=30", cancellationToken));

        if (todos is null)
        {
            return Array.Empty<JsonTodo>();
        }

        _cache.Set(TodosCacheKey, todos, TimeSpan.FromMinutes(5));
        return todos;
    }

    private async Task<IReadOnlyList<JsonUser>> GetUsersAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _cache.TryGetValue(UsersCacheKey, out IReadOnlyList<JsonUser>? cachedUsers) && cachedUsers is not null)
        {
            return cachedUsers;
        }

        var users = await GetWithRetryAsync<List<JsonUser>>(
            () => _httpClient.GetFromJsonAsync<List<JsonUser>>("/users", cancellationToken));

        if (users is null)
        {
            return Array.Empty<JsonUser>();
        }

        _cache.Set(UsersCacheKey, users, TimeSpan.FromMinutes(10));
        return users;
    }

    private static async Task<T?> GetWithRetryAsync<T>(Func<Task<T?>> operation)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt));
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    private static string GetTargetValue(string perspective)
    {
        return perspective switch
        {
            "Financial" => "15%",
            "Customer" => "4.5/5",
            "Internal Process" => "3 days",
            _ => "40 hrs"
        };
    }

    private static string GetActualValue(string perspective, string status, int index)
    {
        var nudge = index % 3;
        return perspective switch
        {
            "Financial" when status == "On Track" => $"{17 + nudge * 0.3:F1}%",
            "Financial" when status == "At Risk" => $"{14 - nudge * 0.4:F1}%",
            "Financial" => $"{11 - nudge * 0.5:F1}%",
            "Customer" when status == "On Track" => "4.7/5",
            "Customer" when status == "At Risk" => "4.3/5",
            "Customer" => "3.9/5",
            "Internal Process" when status == "On Track" => "2.7 days",
            "Internal Process" when status == "At Risk" => "3.8 days",
            "Internal Process" => "4.9 days",
            _ when status == "On Track" => $"{42 + nudge} hrs",
            _ when status == "At Risk" => $"{37 - nudge} hrs",
            _ => $"{31 - nudge} hrs"
        };
    }

    private static IReadOnlyList<KpiTrackingItemViewModel> GetFallbackKpis()
    {
        return new List<KpiTrackingItemViewModel>
        {
            new() { Name = "Revenue Growth Rate", Department = "Finance", Perspective = "Financial", Target = "15%", Actual = "17.2%", Status = "On Track", Severity = AlertSeverity.Standard },
            new() { Name = "Employee Turnover Rate", Department = "HR", Perspective = "Learning & Growth", Target = "40 hrs", Actual = "33 hrs", Status = "At Risk", Severity = AlertSeverity.Warning },
            new() { Name = "Process Cycle Time", Department = "Operations", Perspective = "Internal Process", Target = "3 days", Actual = "5 days", Status = "Behind", Severity = AlertSeverity.Critical }
        };
    }

    private static string ToTitle(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Untitled Item";
        }

        var trimmed = text.Trim();
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string ToRelativeTime(int index)
    {
        var hours = Math.Max(1, (index + 1) * 2);
        if (hours < 24)
        {
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        var days = hours / 24;
        return days == 1 ? "1 day ago" : $"{days} days ago";
    }

    private static IReadOnlyList<NotificationItemViewModel> GetFallbackNotifications()
    {
        return new List<NotificationItemViewModel>
        {
            new() { Title = "KPI Target Achieved", Message = "Revenue Growth Rate exceeded the quarterly target.", Time = "2 hours ago", Read = false, Icon = "bi-check-circle", Severity = AlertSeverity.Standard },
            new() { Title = "KPI At Risk", Message = "Employee Turnover Rate needs intervention.", Time = "4 hours ago", Read = false, Icon = "bi-exclamation-triangle", Severity = AlertSeverity.Warning },
            new() { Title = "Goal Behind Schedule", Message = "Operational initiative fell behind plan.", Time = "6 hours ago", Read = false, Icon = "bi-x-circle", Severity = AlertSeverity.Critical },
            new() { Title = "Report Generated", Message = "Executive Summary for Q1 is ready for review.", Time = "1 day ago", Read = true, Icon = "bi-info-circle", Severity = AlertSeverity.Standard }
        };
    }

    private static IReadOnlyList<ActivityItemViewModel> GetFallbackActivities()
    {
        return new List<ActivityItemViewModel>
        {
            new() { User = "Sarah Johnson", Action = "Updated KPI", Item = "Customer Satisfaction Score", Time = "2 hours ago" },
            new() { User = "Michael Chen", Action = "Logged Entry", Item = "Revenue Growth Rate", Time = "4 hours ago" },
            new() { User = "Emily Davis", Action = "Created Goal", Item = "Q2 Strategic Initiative", Time = "5 hours ago" }
        };
    }

    private sealed class JsonTodo
    {
        public int UserId { get; init; }

        public string Title { get; init; } = string.Empty;

        public bool Completed { get; init; }
    }

    private sealed class JsonUser
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}
