using PeakMetrics.Web.ViewModels;

namespace PeakMetrics.Web.Services;

public static class AlertSeverityRules
{
    public static AlertSeverity FromKpiStatus(string? status)
    {
        if (string.Equals(status, "Behind", StringComparison.OrdinalIgnoreCase))
        {
            return AlertSeverity.Critical;
        }

        if (string.Equals(status, "At Risk", StringComparison.OrdinalIgnoreCase))
        {
            return AlertSeverity.Warning;
        }

        return AlertSeverity.Standard;
    }

    public static AlertSeverity FromNotificationText(string? title, string? message)
    {
        var combined = $"{title} {message}".ToLowerInvariant();

        if (combined.Contains("critical") || combined.Contains("behind") || combined.Contains("failed"))
        {
            return AlertSeverity.Critical;
        }

        if (combined.Contains("risk") || combined.Contains("warning") || combined.Contains("attention"))
        {
            return AlertSeverity.Warning;
        }

        return AlertSeverity.Standard;
    }
}
