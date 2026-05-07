using System.ComponentModel.DataAnnotations;

namespace PeakMetrics.Web.Services;

/// <summary>Static helper for email format and domain validation.</summary>
public static class EmailValidator
{
    private static readonly HashSet<string> BlockedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "peakmetrics.com", "test.com", "example.com",
        "fake.com", "mailinator.com", "tempmail.com", "yopmail.com"
    };

    private static readonly EmailAddressAttribute _emailAttr = new();

    /// <summary>Returns true if the email passes the standard EmailAddress format check.</summary>
    public static bool IsValidFormat(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        return _emailAttr.IsValid(email);
    }

    /// <summary>
    /// Returns true if the email's domain is in the blocked list.
    /// Comparison is case-insensitive.
    /// </summary>
    public static bool IsBlockedDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0) return false;
        var domain = email[(atIndex + 1)..];
        return BlockedDomains.Contains(domain);
    }

    /// <summary>Returns true if the domain portion contains at least one dot.</summary>
    public static bool DomainHasDot(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0) return false;
        var domain = email[(atIndex + 1)..];
        return domain.Contains('.');
    }
}
