using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.ViewModels;

public sealed class KpiManagementListViewModel
{
    public IReadOnlyList<KpiManagementItemViewModel> Kpis { get; init; } = Array.Empty<KpiManagementItemViewModel>();
}

public sealed class KpiManagementItemViewModel
{
    public int    Id           { get; init; }
    public string Name        { get; init; } = string.Empty;
    public string Department  { get; init; } = string.Empty;
    public string Perspective { get; init; } = string.Empty;
    public decimal Target     { get; init; }
    public string Unit        { get; init; } = string.Empty;
    public bool   IsActive    { get; init; }
    public int    LogCount    { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class KpiFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "KPI name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required.")]
    public int DepartmentId { get; set; }

    [Required(ErrorMessage = "Perspective is required.")]
    public string Perspective { get; set; } = string.Empty;

    [Required(ErrorMessage = "Unit is required.")]
    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty;

    [Required(ErrorMessage = "Target value is required.")]
    [Range(0, 9999999, ErrorMessage = "Enter a valid target value.")]
    public decimal Target { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // For dropdowns
    public IReadOnlyList<DepartmentOptionViewModel> Departments { get; set; } = Array.Empty<DepartmentOptionViewModel>();
}

public sealed class DepartmentOptionViewModel
{
    public int    Id   { get; init; }
    public string Name { get; init; } = string.Empty;
}
