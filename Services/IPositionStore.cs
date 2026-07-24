using UcamWaitlistBot.Models;

namespace UcamWaitlistBot.Services;

/// <summary>
/// Persists the waitlist state (last position and last daily-message date) across process
/// restarts so that a redeploy does not re-send notifications and the daily message fires
/// exactly once per calendar day.
/// </summary>
public interface IPositionStore
{
    /// <summary>
    /// Returns the persisted state. If nothing has ever been stored, returns a state with
    /// <see cref="WaitlistState.Position"/> and <see cref="WaitlistState.LastDailyMessageDateLocal"/>
    /// both <c>null</c> (which the worker treats as a first run).
    /// </summary>
    Task<WaitlistState> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>Persists the supplied state, overwriting any previous value.</summary>
    Task SaveStateAsync(WaitlistState state, CancellationToken cancellationToken);
}
