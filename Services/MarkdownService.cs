using Markdig;

namespace PeakMetrics.Web.Services;

/// <summary>
/// Service for converting Markdown to HTML with XSS protection
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Converts markdown text to sanitized HTML
    /// </summary>
    string ToHtml(string? markdown);
}

public class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownService()
    {
        // Configure Markdig pipeline with GitHub Flavored Markdown extensions
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions() // Includes tables, task lists, etc.
            .DisableHtml() // Disable raw HTML for XSS protection
            .Build();
    }

    public string ToHtml(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return Markdown.ToHtml(markdown, _pipeline);
    }
}
