namespace PeakMetrics.Web.Models;

public sealed class Perspective
{
    public int    Id   { get; set; }
    public string Name { get; set; } = string.Empty;

    // Navigation (reverse)
    public ICollection<Kpi>           Kpis  { get; set; } = new List<Kpi>();
    public ICollection<StrategicGoal> Goals { get; set; } = new List<StrategicGoal>();
}
