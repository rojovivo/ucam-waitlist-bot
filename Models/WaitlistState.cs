namespace UcamWaitlistBot.Models;

/// <summary>
/// Persisted state carried across checks and process restarts.
/// </summary>
/// <param name="Position">The last reported waitlist position, or <c>null</c> if never recorded.</param>
/// <param name="LastDailyMessageDateLocal">
/// The local calendar date on which the last daily "morning" message was sent, or <c>null</c> if none.
/// Used to guarantee exactly one morning message per day regardless of restarts.
/// </param>
public sealed record WaitlistState(int? Position, DateOnly? LastDailyMessageDateLocal);
