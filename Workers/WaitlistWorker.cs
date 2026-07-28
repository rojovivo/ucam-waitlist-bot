using Microsoft.Extensions.Options;
using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;
using UcamWaitlistBot.Services;

namespace UcamWaitlistBot.Workers;

/// <summary>
/// Background service that checks the waitlist position on a periodic schedule (with jitter),
/// notifying via Telegram whenever the position changes or on the first run.
/// </summary>
public sealed class WaitlistWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerOptions _options;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<WaitlistWorker> _logger;

    // Set to false after the first successful check, so the optional startup ping fires only once.
    private bool _isFirstCheck = true;

    public WaitlistWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<WaitlistWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Waitlist worker started. RunOnce {RunOnce}, interval {Interval}, active window {Start}-{End} (local), max jitter {Jitter}.",
            _options.RunOnce, _options.PollInterval, _options.ActiveHoursStart, _options.ActiveHoursEnd, _options.MaxJitter);

        // One-shot mode (e.g. GitHub Actions cron): a single check, then exit.
        if (_options.RunOnce)
        {
            var succeeded = await RunCheckWithJitterAsync(stoppingToken).ConfigureAwait(false);
            if (!succeeded)
            {
                // Surface failure to the host process so a scheduled run is flagged as failed.
                Environment.ExitCode = 1;
            }

            _lifetime.StopApplication();
            return;
        }

        // Continuous mode: run once immediately, then on every timer tick.
        using var timer = new PeriodicTimer(_options.PollInterval);
        do
        {
            await RunCheckWithJitterAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    /// <summary>True if the given local time-of-day falls within the configured active window (inclusive).</summary>
    private bool IsWithinActiveHours(TimeSpan timeOfDay) =>
        timeOfDay >= _options.ActiveHoursStart && timeOfDay <= _options.ActiveHoursEnd;

    /// <summary>Runs one check. Returns true on success or an intentional skip, false on failure.</summary>
    private async Task<bool> RunCheckWithJitterAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Skip ticks that fall outside the daily active window (e.g. overnight). Not a failure.
            var now = DateTime.Now;
            if (!IsWithinActiveHours(now.TimeOfDay))
            {
                _logger.LogInformation(
                    "Outside active window {Start}-{End}; skipping check at {Now:t}.",
                    _options.ActiveHoursStart, _options.ActiveHoursEnd, now);
                return true;
            }

            await ApplyJitterAsync(stoppingToken).ConfigureAwait(false);
            await RunCheckAsync(stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown; nothing to report.
            return true;
        }
        catch (Exception ex)
        {
            // A failed check (broken login/selector, timeout after retries, parse failure) must not
            // stop the loop: alert via Telegram and continue.
            _logger.LogError(ex, "Waitlist check failed.");
            await NotifyFailureAsync(ex, stoppingToken).ConfigureAwait(false);
            return false;
        }
    }

    private async Task ApplyJitterAsync(CancellationToken stoppingToken)
    {
        if (_options.MaxJitter <= TimeSpan.Zero)
        {
            return;
        }

        // Random.Shared is thread-safe; the worker runs single-threaded but this is future-proof.
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * _options.MaxJitter.TotalMilliseconds);
        _logger.LogDebug("Applying jitter of {Jitter} before check.", jitter);
        await Task.Delay(jitter, stoppingToken).ConfigureAwait(false);
    }

    private async Task RunCheckAsync(CancellationToken stoppingToken)
    {
        // Resolve per-run (scoped) dependencies from a dedicated scope.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var scraper = scope.ServiceProvider.GetRequiredService<IUcamScraperService>();
        var store = scope.ServiceProvider.GetRequiredService<IPositionStore>();
        var notifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>();

        var result = await scraper.GetWaitlistPositionAsync(stoppingToken).ConfigureAwait(false);
        var state = await store.GetStateAsync(stoppingToken).ConfigureAwait(false);

        var today = DateOnly.FromDateTime(DateTime.Now); // local, matches the active-hours window
        var isFirstProcessCheck = _isFirstCheck;
        _isFirstCheck = false;

        var decision = ReportDecider.Decide(state, result.Estado, result.Position, isFirstProcessCheck, today, _options);

        if (decision.ShouldReport)
        {
            _logger.LogInformation(
                "Reporting ({Reason}): estado '{PrevEstado}'->'{Estado}', position {PrevPos}->{Pos}.",
                decision.Reason, state.LastEstado ?? "(none)", result.Estado,
                state.Position?.ToString() ?? "(none)", result.Position?.ToString() ?? "(none)");

            await notifier.NotifyResultAsync(result, decision.Reason, state, stoppingToken).ConfigureAwait(false);

            // Stamp today's date (daily message once per day) and remember the latest position/estado.
            await store.SaveStateAsync(new WaitlistState(result.Position, today, result.Estado), stoppingToken)
                .ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation(
                "No change (estado '{Estado}', position {Position}); no notification sent.",
                result.Estado, result.Position?.ToString() ?? "(none)");
        }
    }

    private async Task NotifyFailureAsync(Exception ex, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var notifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>();
            await notifier.NotifyErrorAsync(ex.Message, stoppingToken).ConfigureAwait(false);
        }
        catch (Exception notifyEx)
        {
            // If even the notification fails, log and carry on; the next tick will try again.
            _logger.LogError(notifyEx, "Failed to send error notification.");
        }
    }
}
