using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.ViewModels;

public sealed class UserManagementListViewModel
{
    public IReadOnlyList<UserRowDetailViewModel> Users { get; init; } = Array.Empty<UserRowDetailViewModel>();
}

public sealed class UserRowDetailViewModel
{
    public int     Id             { get; init; }
    public string  FullName       { get; init; } = string.Empty;
    public string  Email          { get; init; } = string.Empty;
    public string  Role           { get; init; } = string.Empty;
    public string? DepartmentName { get; init; }
    public string  CreatedAt      { get; init; } = string.Empty;
    public bool    IsActive       { get; init; }
    public string? LastLoginAt    { get; init; }
}

public sealed class UserFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public string Email { get; set; } = string.Empty;

    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Role is required.")]
    public string Role { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }

    public bool IsActive { get; set; } = true;

    // For dropdown
    public IReadOnlyList<DepartmentOptionViewModel> Departments { get; set; } = Array.Empty<DepartmentOptionViewModel>();
}
