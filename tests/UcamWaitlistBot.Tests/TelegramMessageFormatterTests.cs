using UcamWaitlistBot.Services;
using UcamWaitlistBot.Workers;
using Xunit;

namespace UcamWaitlistBot.Tests;

public class TelegramMessageFormatterTests
{
    [Fact]
    public void Initial_message_shows_current_position_and_estado()
    {
        var text = TelegramMessageFormatter.BuildMessage(
            "Grado en Fisioterapia", "En espera", 16, ReportReason.Initial, previousEstado: null);

        Assert.Contains("Current waitlist position", text);
        Assert.Contains("POSICIÓN DE ESPERA: *16*", text);
        Assert.Contains("En espera", text);
        Assert.Contains("Grado en Fisioterapia", text);
    }

    [Fact]
    public void PositionChanged_uses_changed_headline()
    {
        var text = TelegramMessageFormatter.BuildMessage(
            "Grado en Fisioterapia", "En espera", 14, ReportReason.PositionChanged, "En espera");

        Assert.Contains("Waitlist position changed", text);
        Assert.Contains("*14*", text);
    }

    [Fact]
    public void Admission_message_celebrates_and_handles_null_position()
    {
        var text = TelegramMessageFormatter.BuildMessage(
            "Grado en Fisioterapia", "Admitido", null, ReportReason.Admission, "En espera");

        Assert.Contains("admitted", text);
        Assert.Contains("Admitido", text);
        Assert.Contains("Grado en Fisioterapia", text);
        Assert.Contains("—", text); // null position rendered as em dash
    }

    [Fact]
    public void StatusChanged_shows_old_to_new()
    {
        var text = TelegramMessageFormatter.BuildMessage(
            "Grado en Fisioterapia", "En espera (revisión)", 16, ReportReason.StatusChanged, "En espera");

        Assert.Contains("Status changed", text);
        Assert.Contains("En espera", text);
        Assert.Contains("En espera (revisión)", text);
    }

    [Fact]
    public void PositionText_renders_number_or_em_dash()
    {
        Assert.Equal("16", TelegramMessageFormatter.PositionText(16));
        Assert.Equal("—", TelegramMessageFormatter.PositionText(null));
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
