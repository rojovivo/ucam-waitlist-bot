using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using UcamWaitlistBot.Configuration;

namespace UcamWaitlistBot.Services;

/// <summary>
/// <see cref="ITelegramNotifier"/> backed by <see cref="ITelegramBotClient"/>.
/// </summary>
public sealed class TelegramNotifier : ITelegramNotifier
{
    private readonly ITelegramBotClient _botClient;
    private readonly long _chatId;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(
        ITelegramBotClient botClient,
        IOptions<TelegramOptions> options,
        ILogger<TelegramNotifier> logger)
    {
        _botClient = botClient;
        _chatId = options.Value.ChatId;
        _logger = logger;
    }

    public async Task NotifyPositionAsync(string programName, int position, bool isFirstRun, CancellationToken cancellationToken)
    {
        var headline = isFirstRun ? "📋 Current waitlist position" : "🔔 Waitlist position changed";
        var text =
            $"{headline}\n" +
            $"*{Escape(programName)}*\n" +
            $"POSICIÓN DE ESPERA: *{position}*";

        await SendAsync(text, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Sent position notification: {Program} -> {Position}.", programName, position);
    }

    public async Task NotifyErrorAsync(string message, CancellationToken cancellationToken)
    {
        var text = $"⚠️ *UCAM waitlist bot check failed*\n{Escape(message)}";
        await SendAsync(text, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Sent error notification to chat {ChatId}.", _chatId);
    }

    private Task SendAsync(string text, CancellationToken cancellationToken) =>
        _botClient.SendMessage(
            chatId: _chatId,
            text: text,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);

    /// <summary>Escapes the characters that are significant in Telegram's (legacy) Markdown mode.</summary>
    private static string Escape(string value) =>
        value.Replace("_", "\\_")
             .Replace("*", "\\*")
             .Replace("[", "\\[")
             .Replace("`", "\\`");
}
