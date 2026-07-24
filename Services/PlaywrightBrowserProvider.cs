using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using UcamWaitlistBot.Configuration;

namespace UcamWaitlistBot.Services;

/// <summary>
/// Lazily launches Chromium and keeps it alive for the lifetime of the application.
/// Registered as a singleton so every check reuses the same browser process.
/// </summary>
public sealed class PlaywrightBrowserProvider : IBrowserProvider
{
    private readonly bool _headless;
    private readonly ILogger<PlaywrightBrowserProvider> _logger;
    private readonly SemaphoreSlim _initGate = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightBrowserProvider(IOptions<WorkerOptions> options, ILogger<PlaywrightBrowserProvider> logger)
    {
        _headless = options.Value.Headless;
        _logger = logger;
    }

    public async Task<IBrowser> GetBrowserAsync()
    {
        // Fast path: already launched and still connected.
        if (_browser is { IsConnected: true })
        {
            return _browser;
        }

        await _initGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_browser is { IsConnected: true })
            {
                return _browser;
            }

            // Dispose a dead browser before relaunching (e.g. the process crashed between checks).
            if (_browser is not null)
            {
                await _browser.DisposeAsync().ConfigureAwait(false);
                _browser = null;
            }

            _playwright ??= await Playwright.CreateAsync().ConfigureAwait(false);

            _logger.LogInformation("Launching Chromium (headless: {Headless}).", _headless);
            _browser = await _playwright.Chromium
                .LaunchAsync(new BrowserTypeLaunchOptions { Headless = _headless })
                .ConfigureAwait(false);

            return _browser;
        }
        finally
        {
            _initGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();
        _initGate.Dispose();
    }
}
