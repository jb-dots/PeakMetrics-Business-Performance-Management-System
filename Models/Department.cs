using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.Models;

public sealed class Department
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Department name is required.")]
    [StringLength(50, ErrorMessage = "Department name cannot exceed 50 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; } = false;

    // Navigation
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<Kpi> Kpis { get; set; } = new List<Kpi>();
}
