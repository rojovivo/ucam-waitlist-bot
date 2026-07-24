using Microsoft.Playwright;

namespace UcamWaitlistBot.Services;

/// <summary>
/// Owns the lifetime of the shared Chromium instance. The browser is launched once and reused
/// across checks; each check gets a fresh, isolated context.
/// </summary>
public interface IBrowserProvider : IAsyncDisposable
{
    /// <summary>Returns the shared browser, launching it on first use.</summary>
    Task<IBrowser> GetBrowserAsync();
}
