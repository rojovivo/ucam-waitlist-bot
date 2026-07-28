using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;
using UcamWaitlistBot.Workers;

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

    public async Task NotifyResultAsync(WaitlistResult result, ReportReason reason, WaitlistState previous, CancellationToken cancellationToken)
    {
        var text = TelegramMessageFormatter.BuildMessage(
            result.ProgramName, result.Estado, result.Position, reason, previous.LastEstado);
        await SendAsync(text, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Sent notification ({Reason}): {Program} estado '{Estado}', position {Position}.",
            reason, result.ProgramName, result.Estado, result.Position?.ToString() ?? "(none)");
    }

    public async Task NotifyErrorAsync(string message, CancellationToken cancellationToken)
    {
        var text = TelegramMessageFormatter.BuildErrorMessage(message);
        await SendAsync(text, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Sent error notification to chat {ChatId}.", _chatId);
    }

    private Task SendAsync(string text, CancellationToken cancellationToken) =>
        _botClient.SendMessage(
            chatId: _chatId,
            text: text,
            parseMode: ParseMode.Markdown,
            cancellationToken: cancellationToken);
}
