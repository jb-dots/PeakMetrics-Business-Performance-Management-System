using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using PeakMetrics.Web.Services;

namespace PeakMetrics.Web.Helpers;

/// <summary>
/// HTML helper extension methods for rendering Markdown content
/// </summary>
public static class MarkdownHelper
{
    /// <summary>
    /// Renders markdown text as sanitized HTML
    /// </summary>
    /// <param name="html">The HTML helper instance</param>
    /// <param name="markdown">The markdown text to render</param>
    /// <returns>HTML content wrapped in a div with markdown-content class</returns>
    public static IHtmlContent RenderMarkdown(this IHtmlHelper html, string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return HtmlString.Empty;
        }

        var markdownService = html.ViewContext.HttpContext.RequestServices
            .GetRequiredService<IMarkdownService>();

        var htmlContent = markdownService.ToHtml(markdown);
        
        // Wrap in a div with markdown-content class for styling
        return new HtmlString($"<div class=\"markdown-content\">{htmlContent}</div>");
    }
}
