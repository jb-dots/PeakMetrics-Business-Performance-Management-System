namespace PeakMetrics.Web.Models;

public sealed class GoalKpi
{
    public int GoalId { get; set; }
    public int KpiId  { get; set; }

    // Navigation
    public StrategicGoal Goal { get; set; } = null!;
    public Kpi           Kpi  { get; set; } = null!;
}
