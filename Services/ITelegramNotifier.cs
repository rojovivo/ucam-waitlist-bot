using UcamWaitlistBot.Models;
using UcamWaitlistBot.Workers;

namespace UcamWaitlistBot.Services;

/// <summary>
/// Sends notifications to the configured Telegram chat.
/// </summary>
public interface ITelegramNotifier
{
    /// <summary>
    /// Notifies about a scraped result, choosing the message wording from the decision
    /// <paramref name="reason"/> and the <paramref name="previous"/> state (for status transitions).
    /// </summary>
    Task NotifyResultAsync(WaitlistResult result, ReportReason reason, WaitlistState previous, CancellationToken cancellationToken);

    /// <summary>Notifies that a check failed, including a short human-readable reason.</summary>
    Task NotifyErrorAsync(string message, CancellationToken cancellationToken);
}
