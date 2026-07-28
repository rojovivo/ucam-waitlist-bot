using System.Text.RegularExpressions;
using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;

namespace UcamWaitlistBot.Workers;

/// <summary>Why a check produced a notification (drives message wording; highest-priority reason wins).</summary>
public enum ReportReason
{
    /// <summary>No message should be sent.</summary>
    None,

    /// <summary>First run, daily "good morning", or startup ping — a plain current-status report.</summary>
    Initial,

    /// <summary>The waitlist position number changed.</summary>
    PositionChanged,

    /// <summary>The Estado text changed (but still on the waitlist).</summary>
    StatusChanged,

    /// <summary>Estado moved away from "En espera" — a likely admission.</summary>
    Admission,
}

/// <summary>Outcome of the reporting decision for a single check.</summary>
public readonly record struct ReportDecision(bool ShouldReport, ReportReason Reason);

/// <summary>
/// Pure decision logic for whether a check should produce a Telegram message and why. Extracted from
/// the worker so it can be unit-tested without Playwright/Telegram/state I/O.
/// </summary>
public static class ReportDecider
{
    private static readonly Regex OnWaitlist = new(@"en\s*espera", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Decides whether to notify, given the previously persisted state, the freshly scraped estado and
    /// position, whether this is the first check of the process, today's (local) date, and options.
    /// Priority: Admission &gt; StatusChanged &gt; PositionChanged &gt; Initial (first run / daily / startup).
    /// </summary>
    public static ReportDecision Decide(
        WaitlistState previous,
        string currentEstado,
        int? currentPosition,
        bool isFirstProcessCheck,
        DateOnly today,
        WorkerOptions options)
    {
        var isEmptyPrevious = previous.Position is null && previous.LastEstado is null;

        var currentlyOnWaitlist = OnWaitlist.IsMatch(currentEstado);
        var previouslyOnWaitlist = previous.LastEstado is null || OnWaitlist.IsMatch(previous.LastEstado);

        // Leaving the waitlist is the headline event (likely admission).
        var admission = previouslyOnWaitlist && !currentlyOnWaitlist;

        // Any other change to the estado text (only meaningful once we have a prior value).
        var estadoChanged = previous.LastEstado is not null
            && !string.Equals(previous.LastEstado.Trim(), currentEstado.Trim(), StringComparison.OrdinalIgnoreCase);

        // Position change only counts when both values are numeric.
        var positionChanged = previous.Position is not null && currentPosition is not null
            && previous.Position.Value != currentPosition.Value;

        // Once-per-process startup ping and once-per-day "good morning" message.
        var startupPing = options.NotifyOnStartup && isFirstProcessCheck;
        var dailyDue = options.SendDailyMorningMessage && previous.LastDailyMessageDateLocal != today;
        var initial = isEmptyPrevious || startupPing || dailyDue;

        var reason = admission ? ReportReason.Admission
            : estadoChanged ? ReportReason.StatusChanged
            : positionChanged ? ReportReason.PositionChanged
            : initial ? ReportReason.Initial
            : ReportReason.None;

        return new ReportDecision(reason != ReportReason.None, reason);
    }
}
