using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.ViewModels;

public sealed class KpiFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "KPI name is required.")]
    [MaxLength(200, ErrorMessage = "Name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required.")]
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "Perspective is required.")]
    public int PerspectiveId { get; set; }

    [Required(ErrorMessage = "Unit is required.")]
    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Target value is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "Target must be a positive number.")]
    public decimal Target { get; set; }

    [MaxLength(100)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // For dropdowns
    public IReadOnlyList<DepartmentOptionViewModel> Departments  { get; set; } = Array.Empty<DepartmentOptionViewModel>();
    public IReadOnlyList<PerspectiveOptionViewModel> Perspectives { get; set; } = Array.Empty<PerspectiveOptionViewModel>();
}

/// <summary>Lightweight option used in Department dropdowns.</summary>
public sealed class DepartmentOptionViewModel
{
    public int    Id   { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>Lightweight option used in Perspective dropdowns.</summary>
public sealed class PerspectiveOptionViewModel
{
    public int    Id   { get; init; }
    public string Name { get; init; } = string.Empty;
}
