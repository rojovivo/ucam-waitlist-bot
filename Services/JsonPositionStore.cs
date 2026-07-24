using System.Text.Json;
using Microsoft.Extensions.Options;
using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;

namespace UcamWaitlistBot.Services;

/// <summary>
/// File-backed <see cref="IPositionStore"/> that stores the last position as a small JSON document.
/// </summary>
public sealed class JsonPositionStore : IPositionStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILogger<JsonPositionStore> _logger;
    // Serialises concurrent reads/writes; the worker is single-threaded but this keeps the store safe.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonPositionStore(IOptions<WorkerOptions> options, ILogger<JsonPositionStore> logger)
    {
        _filePath = Path.GetFullPath(options.Value.StateFilePath);
        _logger = logger;
    }

    /// <summary>Shape of the persisted document.</summary>
    private sealed record StoredState(
        int? Position,
        DateOnly? LastDailyMessageDateLocal,
        DateTimeOffset UpdatedAtUtc);

    public async Task<WaitlistState> GetStateAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("No state file at {Path}; treating next check as first run.", _filePath);
                return new WaitlistState(null, null);
            }

            await using var stream = File.OpenRead(_filePath);
            var state = await JsonSerializer
                .DeserializeAsync<StoredState>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return state is null
                ? new WaitlistState(null, null)
                : new WaitlistState(state.Position, state.LastDailyMessageDateLocal);
        }
        catch (JsonException ex)
        {
            // A corrupt state file should not crash the bot; treat it as "no known value".
            _logger.LogWarning(ex, "State file at {Path} is unreadable; treating next check as first run.", _filePath);
            return new WaitlistState(null, null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveStateAsync(WaitlistState state, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var stored = new StoredState(state.Position, state.LastDailyMessageDateLocal, DateTimeOffset.UtcNow);

            // Write to a temp file then move, so a crash mid-write cannot leave a truncated document.
            var tempPath = _filePath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, stored, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, _filePath, overwrite: true);
            _logger.LogDebug("Persisted state (position {Position}, dailyDate {Date}) to {Path}.",
                state.Position, state.LastDailyMessageDateLocal, _filePath);
        }
        finally
        {
            _gate.Release();
        }
    }
}
