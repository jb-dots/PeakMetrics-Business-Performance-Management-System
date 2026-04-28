namespace PeakMetrics.Web.Models;

public sealed class Department
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; } = false;

    // Navigation
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<Kpi> Kpis { get; set; } = new List<Kpi>();
}
