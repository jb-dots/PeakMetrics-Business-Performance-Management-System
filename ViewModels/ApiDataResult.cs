namespace PeakMetrics.Web.ViewModels;

public sealed class ApiDataResult<T>
{
    public T Data { get; init; } = default!;

    public bool IsFallback { get; init; }

    public string Message { get; init; } = string.Empty;
}
