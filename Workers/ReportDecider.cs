using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;

namespace UcamWaitlistBot.Workers;

/// <summary>Outcome of the reporting decision for a single check.</summary>
/// <param name="ShouldReport">Whether a Telegram message should be sent for this check.</param>
/// <param name="ReportAsChange">
/// True if the report is due to the position changing (drives "changed" vs "current position" wording).
/// </param>
public readonly record struct ReportDecision(bool ShouldReport, bool ReportAsChange);

/// <summary>
/// Pure decision logic for whether a check should produce a Telegram message. Extracted from the
/// worker so it can be unit-tested without Playwright/Telegram/state I/O.
/// </summary>
public static class ReportDecider
{
    /// <summary>
    /// Decides whether to notify, given the previously persisted state, the freshly scraped
    /// position, whether this is the first check of the process, today's (local) date, and options.
    /// </summary>
    public static ReportDecision Decide(
        WaitlistState previous,
        int currentPosition,
        bool isFirstProcessCheck,
        DateOnly today,
        WorkerOptions options)
    {
        var isFirstRun = previous.Position is null;
        var changed = !isFirstRun && previous.Position!.Value != currentPosition;

        // Once-per-process startup ping (confirms connectivity even when unchanged).
        var startupPing = options.NotifyOnStartup && isFirstProcessCheck;

        // Once-per-day "good morning" message on the first in-window check of a new calendar day.
        var dailyDue = options.SendDailyMorningMessage && previous.LastDailyMessageDateLocal != today;

        var shouldReport = isFirstRun || changed || startupPing || dailyDue;
        return new ReportDecision(shouldReport, changed);
    }
}
