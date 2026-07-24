using System.ComponentModel.DataAnnotations;

namespace UcamWaitlistBot.Configuration;

/// <summary>
/// Scheduling and browser behaviour for the background worker.
/// Bound from the "Worker" section of configuration.
/// </summary>
public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>Base interval between checks. Defaults to 1 hour.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "24:00:00")]
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Start of the daily active window (inclusive), in the machine's local time. Checks outside
    /// [ActiveHoursStart, ActiveHoursEnd] are skipped. Defaults to 08:00.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "23:59:59")]
    public TimeSpan ActiveHoursStart { get; init; } = TimeSpan.FromHours(8);

    /// <summary>
    /// End of the daily active window (inclusive), in the machine's local time. Defaults to 20:00.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "23:59:59")]
    public TimeSpan ActiveHoursEnd { get; init; } = TimeSpan.FromHours(20);

    /// <summary>
    /// Upper bound of the random delay added before each check so the polling pattern is not strictly periodic.
    /// A value of 00:00:00 disables jitter.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:10:00")]
    public TimeSpan MaxJitter { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>Relative or absolute path of the JSON file used to persist the last known position.</summary>
    [Required]
    public string StateFilePath { get; init; } = "state/last-position.json";

    /// <summary>Whether Chromium runs headless. Set to false locally to observe/tune selectors.</summary>
    public bool Headless { get; init; } = true;

    /// <summary>
    /// When true, the first successful check after startup always sends a Telegram message with the
    /// current position (even if unchanged), confirming end-to-end connectivity. Set to false to only
    /// be notified on actual changes.
    /// </summary>
    public bool NotifyOnStartup { get; init; } = true;

    /// <summary>
    /// When true, sends the current position once per calendar day on the first in-window check
    /// (a "good morning" message), regardless of whether it changed. Set to false to only be
    /// notified on actual changes.
    /// </summary>
    public bool SendDailyMorningMessage { get; init; } = true;

    /// <summary>Timeout applied to individual Playwright navigation and locator waits.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:05:00")]
    public TimeSpan NavigationTimeout { get; init; } = TimeSpan.FromSeconds(45);

    /// <summary>
    /// When true, the worker performs a single check and then exits instead of looping. Used for
    /// scheduled/one-shot hosting (e.g. GitHub Actions cron) where the platform provides the schedule.
    /// </summary>
    public bool RunOnce { get; init; } = false;
}
