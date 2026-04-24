namespace PeakMetrics.Web.Models;

public sealed class Kpi
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Perspective { get; set; } = string.Empty; // Financial | Customer | Internal Process | Learning & Growth

    public string Unit { get; set; } = string.Empty; // %, score, days, hrs, etc.

    public decimal Target { get; set; }

    public string? Description { get; set; }

    public int DepartmentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation
    public Department Department { get; set; } = null!;
    public ICollection<KpiLogEntry> LogEntries { get; set; } = new List<KpiLogEntry>();
}
