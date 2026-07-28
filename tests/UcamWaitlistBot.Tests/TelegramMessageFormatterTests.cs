using UcamWaitlistBot.Services;
using Xunit;

namespace UcamWaitlistBot.Tests;

public class TelegramMessageFormatterTests
{
    [Fact]
    public void PositionMessage_firstRun_uses_current_position_headline()
    {
        var text = TelegramMessageFormatter.BuildPositionMessage("Grado en Fisioterapia", 16, isFirstRun: true);

        Assert.Contains("Current waitlist position", text);
        Assert.Contains("POSICIÓN DE ESPERA: *16*", text);
        Assert.Contains("Grado en Fisioterapia", text);
    }

    [Fact]
    public void PositionMessage_change_uses_changed_headline()
    {
        var text = TelegramMessageFormatter.BuildPositionMessage("Grado en Fisioterapia", 14, isFirstRun: false);

        Assert.Contains("Waitlist position changed", text);
        Assert.Contains("*14*", text);
    }

    [Fact]
    public void ErrorMessage_contains_failure_header_and_message()
    {
        var text = TelegramMessageFormatter.BuildErrorMessage("Timeout after retries");

        Assert.Contains("check failed", text);
        Assert.Contains("Timeout after retries", text);
    }

    [Theory]
    [InlineData("a_b", "a\\_b")]
    [InlineData("a*b", "a\\*b")]
    [InlineData("a[b", "a\\[b")]
    [InlineData("a`b", "a\\`b")]
    public void Escape_escapes_markdown_specials(string input, string expected)
    {
        Assert.Equal(expected, TelegramMessageFormatter.Escape(input));
    }
}
