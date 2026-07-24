using UcamWaitlistBot.Models;

namespace UcamWaitlistBot.Services;

/// <summary>
/// Drives the browser through the portal and returns the current waitlist position.
/// </summary>
public interface IUcamScraperService
{
    /// <summary>
    /// Logs in, opens the configured program, navigates to the results step and reads the
    /// "POSICIÓN DE ESPERA" value. Internally resilient to transient timeouts.
    /// </summary>
    Task<WaitlistResult> GetWaitlistPositionAsync(CancellationToken cancellationToken);
}
