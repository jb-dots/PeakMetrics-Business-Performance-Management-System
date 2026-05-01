namespace PeakMetrics.Web.Models;

public sealed class Kpi
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int PerspectiveId { get; set; }

    public string Unit { get; set; } = string.Empty; // %, score, days, hrs, etc.

    public decimal Target { get; set; }

    public string? Description { get; set; }

    public int DepartmentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    /// <summary>Monthly | Quarterly | Annual</summary>
    public string Frequency { get; set; } = "Monthly";

    /// <summary>On Track | At Risk | Behind</summary>
    public string Status { get; set; } = "On Track";

    public int? CreatedByUserId { get; set; }

    // Navigation
    public Perspective Perspective { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public AppUser? CreatedBy { get; set; }
    public ICollection<KpiLogEntry> LogEntries { get; set; } = new List<KpiLogEntry>();
    public ICollection<StrategicGoal> LinkedGoals { get; set; } = new List<StrategicGoal>();
}
