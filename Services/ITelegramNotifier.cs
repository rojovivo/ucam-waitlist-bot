namespace UcamWaitlistBot.Services;

/// <summary>
/// Sends notifications to the configured Telegram chat.
/// </summary>
public interface ITelegramNotifier
{
    /// <summary>Notifies that the waitlist position is now <paramref name="position"/>.</summary>
    Task NotifyPositionAsync(string programName, int position, bool isFirstRun, CancellationToken cancellationToken);

    /// <summary>Notifies that a check failed, including a short human-readable reason.</summary>
    Task NotifyErrorAsync(string message, CancellationToken cancellationToken);
}
