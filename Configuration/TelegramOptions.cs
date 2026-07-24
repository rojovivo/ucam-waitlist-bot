using System.ComponentModel.DataAnnotations;

namespace UcamWaitlistBot.Configuration;

/// <summary>
/// Telegram bot credentials. Bound from the "Telegram" section of configuration.
/// </summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>Bot token issued by BotFather. Provide via user-secrets or environment variables.</summary>
    [Required]
    public string BotToken { get; init; } = string.Empty;

    /// <summary>Numeric chat id that receives the notifications.</summary>
    [Range(long.MinValue, long.MaxValue, ErrorMessage = "A non-zero Telegram ChatId is required.")]
    public long ChatId { get; init; }
}
