using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.ViewModels;

public sealed class DepartmentManagementViewModel
{
    public IReadOnlyList<DepartmentRowViewModel> Departments { get; init; } = Array.Empty<DepartmentRowViewModel>();
}

public sealed class DepartmentRowViewModel
{
    public int     Id          { get; init; }
    public string  Name        { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int     UserCount   { get; init; }
    public int     KpiCount    { get; init; }
    public string  CreatedAt   { get; init; } = string.Empty;
}

public sealed class DepartmentFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Department name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
}
