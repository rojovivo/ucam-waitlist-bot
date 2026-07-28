using UcamWaitlistBot.Workers;

namespace UcamWaitlistBot.Services;

/// <summary>
/// Pure builders for the Telegram message text. Extracted from <see cref="TelegramNotifier"/> so
/// the formatting (and Markdown escaping) can be unit-tested without sending anything.
/// </summary>
public static class TelegramMessageFormatter
{
    /// <summary>Builds the notification body for a reported result, per the decision reason.</summary>
    public static string BuildMessage(
        string programName,
        string estado,
        int? position,
        ReportReason reason,
        string? previousEstado)
    {
        var program = $"*{Escape(programName)}*";
        var positionLine = $"POSICIÓN DE ESPERA: *{PositionText(position)}*";

        return reason switch
        {
            ReportReason.Admission =>
                $"🎉 *You may have been admitted!*\n{program}\nESTADO: *{Escape(estado)}*\n{positionLine}",

            ReportReason.StatusChanged =>
                $"🔔 *Status changed*\n{program}\n{Escape(previousEstado ?? "?")} → *{Escape(estado)}*\n{positionLine}",

            ReportReason.PositionChanged =>
                $"🔔 Waitlist position changed\n{program}\nESTADO: {Escape(estado)}\n{positionLine}",

            // Initial / daily / startup.
            _ =>
                $"📋 Current waitlist position\n{program}\nESTADO: {Escape(estado)}\n{positionLine}",
        };
    }

    public static string BuildErrorMessage(string message) =>
        $"⚠️ *UCAM waitlist bot check failed*\n{Escape(message)}";

    /// <summary>Renders the position, or an em dash when there is no numeric value (e.g. after admission).</summary>
    public static string PositionText(int? position) => position?.ToString() ?? "—";

    /// <summary>Escapes the characters that are significant in Telegram's (legacy) Markdown mode.</summary>
    public static string Escape(string value) =>
        value.Replace("_", "\\_")
             .Replace("*", "\\*")
             .Replace("[", "\\[")
             .Replace("`", "\\`");
}
