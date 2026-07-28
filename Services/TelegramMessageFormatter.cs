namespace UcamWaitlistBot.Services;

/// <summary>
/// Pure builders for the Telegram message text. Extracted from <see cref="TelegramNotifier"/> so
/// the formatting (and Markdown escaping) can be unit-tested without sending anything.
/// </summary>
public static class TelegramMessageFormatter
{
    public static string BuildPositionMessage(string programName, int position, bool isFirstRun)
    {
        var headline = isFirstRun ? "📋 Current waitlist position" : "🔔 Waitlist position changed";
        return $"{headline}\n" +
               $"*{Escape(programName)}*\n" +
               $"POSICIÓN DE ESPERA: *{position}*";
    }

    public static string BuildErrorMessage(string message) =>
        $"⚠️ *UCAM waitlist bot check failed*\n{Escape(message)}";

    /// <summary>Escapes the characters that are significant in Telegram's (legacy) Markdown mode.</summary>
    public static string Escape(string value) =>
        value.Replace("_", "\\_")
             .Replace("*", "\\*")
             .Replace("[", "\\[")
             .Replace("`", "\\`");
}
