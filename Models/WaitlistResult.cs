namespace UcamWaitlistBot.Models;

/// <summary>
/// Outcome of a single scrape: the waitlist position for a program at a point in time.
/// </summary>
/// <param name="ProgramName">The program the position belongs to.</param>
/// <param name="Position">The integer value read from the "POSICIÓN DE ESPERA" column.</param>
/// <param name="ScrapedAtUtc">When the value was captured.</param>
public sealed record WaitlistResult(string ProgramName, int Position, DateTimeOffset ScrapedAtUtc);
