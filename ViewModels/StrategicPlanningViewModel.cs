namespace PeakMetrics.Web.ViewModels;

public sealed class StrategicPlanningViewModel
{
    public IReadOnlyList<StrategicGoalCardViewModel> Goals { get; init; } = Array.Empty<StrategicGoalCardViewModel>();
}

public sealed class StrategicGoalCardViewModel
{
    public int     Id          { get; init; }
    public string  Title       { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string  Perspective { get; init; } = string.Empty;
    public string  Status      { get; init; } = "Not Started";
    public string? DueDate     { get; init; }
    public string? OwnerName   { get; init; }
}

public sealed class StrategicGoalFormViewModel
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Title is required.")]
    [System.ComponentModel.DataAnnotations.MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Perspective is required.")]
    public string Perspective { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Status is required.")]
    public string Status { get; set; } = "Not Started";

    public DateTime? DueDate { get; set; }
}
