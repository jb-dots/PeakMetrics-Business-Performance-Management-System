using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.Models;

public sealed class StrategicGoal
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(300, ErrorMessage = "Title cannot exceed 300 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int PerspectiveId { get; set; }

    /// <summary>Not Started | In Progress | Completed | Cancelled</summary>
    public string Status { get; set; } = "Not Started";

    [Range(1, 9999, ErrorMessage = "Target year must be between 1 and 9999.")]
    public int? TargetYear { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? OwnerUserId { get; set; }

    public bool IsArchived { get; set; } = false;

    // Navigation
    public Perspective Perspective { get; set; } = null!;
    public AppUser? Owner { get; set; }
    public ICollection<Kpi> LinkedKpis { get; set; } = new List<Kpi>();
}
