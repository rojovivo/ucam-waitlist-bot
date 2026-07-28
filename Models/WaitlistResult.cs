namespace UcamWaitlistBot.Models;

/// <summary>
/// Outcome of a single scrape: the admission status and waitlist position for a program at a point in time.
/// </summary>
/// <param name="ProgramName">The program the result belongs to.</param>
/// <param name="Estado">The raw text of the "Estado" column (e.g. "En espera").</param>
/// <param name="Position">
/// The integer value under "Posición de espera", or <c>null</c> when it is absent/non-numeric
/// (e.g. once admission removes the waitlist number).
/// </param>
/// <param name="ScrapedAtUtc">When the value was captured.</param>
public sealed record WaitlistResult(string ProgramName, string Estado, int? Position, DateTimeOffset ScrapedAtUtc);
