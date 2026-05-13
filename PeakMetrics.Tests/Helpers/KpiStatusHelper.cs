// ============================================
// PeakMetrics - KPI Status Helper (Test Mirror)
// IT16 Information Assurance and Security 1
// Student: John Benedic F. Dutaro
// Date: 2026
// Description: Mirrors the ComputeStatus logic from
//              HomeController so KPI tests can call it
//              directly without spinning up the full MVC stack.
// ============================================

using PeakMetrics.Web.Models;

namespace PeakMetrics.Tests.Helpers;

/// <summary>
/// Mirrors the ComputeStatus business logic from HomeController.
/// Kept in sync with the production implementation.
/// </summary>
public static class KpiStatusHelper
{
    public const string OnTrack = "On Track";
    public const string AtRisk  = "At Risk";
    public const string Behind  = "Behind";

    /// <summary>
    /// Determines whether a KPI is "lower is better" based on its unit or name.
    /// Mirrors the production logic in HomeController.ComputeStatus.
    /// </summary>
    private static bool IsLowerBetter(Kpi kpi)
    {
        var unit = kpi.Unit.ToLowerInvariant();
        var name = kpi.Name.ToLowerInvariant();

        return unit == "days"
            || name.Contains("turnover")
            || name.Contains("defect")
            || name.Contains("cycle");
    }

    /// <summary>
    /// Computes the KPI status string given a KPI definition and an actual value.
    /// </summary>
    public static string ComputeStatus(Kpi kpi, decimal actual)
    {
        if (kpi.Target == 0) return OnTrack;

        if (IsLowerBetter(kpi))
        {
            // Lower is better: on track when actual <= target
            if (actual <= kpi.Target)                    return OnTrack;
            if (actual <= kpi.Target * 1.25m)            return AtRisk;
            return Behind;
        }
        else
        {
            // Higher is better: on track when actual >= target
            if (actual >= kpi.Target)                    return OnTrack;
            if (actual >= kpi.Target * 0.75m)            return AtRisk;
            return Behind;
        }
    }
}
