using System.ComponentModel.DataAnnotations;

namespace UcamWaitlistBot.Configuration;

/// <summary>
/// Credentials and target information for the UCAM admissions portal.
/// Bound from the "Portal" section of configuration.
/// </summary>
public sealed class PortalOptions
{
    public const string SectionName = "Portal";

    /// <summary>The SPA login URL.</summary>
    [Required]
    [Url]
    public string LoginUrl { get; init; } = string.Empty;

    /// <summary>Portal username. Provide via user-secrets or environment variables, never source control.</summary>
    [Required]
    public string Username { get; init; } = string.Empty;

    /// <summary>Portal password. Provide via user-secrets or environment variables, never source control.</summary>
    [Required]
    public string Password { get; init; } = string.Empty;

    /// <summary>Display name of the program whose waitlist position we track, e.g. "Grado en Fisioterapia".</summary>
    [Required]
    public string ProgramName { get; init; } = string.Empty;
}
