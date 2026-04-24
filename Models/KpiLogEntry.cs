namespace PeakMetrics.Web.Models;

public sealed class KpiLogEntry
{
    public int Id { get; set; }

    public int KpiId { get; set; }

    public int LoggedByUserId { get; set; }

    public decimal ActualValue { get; set; }

    /// <summary>On Track | At Risk | Behind</summary>
    public string Status { get; set; } = "On Track";

    public string? Notes { get; set; }

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    public string Period { get; set; } = string.Empty; // e.g. "Q1 2025", "April 2025"

    // Navigation
    public Kpi Kpi { get; set; } = null!;
    public AppUser LoggedBy { get; set; } = null!;
}
