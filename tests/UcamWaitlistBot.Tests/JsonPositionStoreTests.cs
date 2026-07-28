using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;
using UcamWaitlistBot.Services;
using Xunit;

namespace UcamWaitlistBot.Tests;

public sealed class JsonPositionStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public JsonPositionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ucam-store-tests", Guid.NewGuid().ToString("N"));
        _file = Path.Combine(_dir, "state", "last-position.json");
    }

    private JsonPositionStore CreateStore()
    {
        var options = Options.Create(new WorkerOptions { StateFilePath = _file });
        return new JsonPositionStore(options, NullLogger<JsonPositionStore>.Instance);
    }

    [Fact]
    public async Task Save_then_get_round_trips()
    {
        var store = CreateStore();
        var date = new DateOnly(2026, 7, 28);

        await store.SaveStateAsync(new WaitlistState(16, date, "En espera"), CancellationToken.None);
        var loaded = await store.GetStateAsync(CancellationToken.None);

        Assert.Equal(16, loaded.Position);
        Assert.Equal(date, loaded.LastDailyMessageDateLocal);
        Assert.Equal("En espera", loaded.LastEstado);
    }

    [Fact]
    public async Task Missing_file_returns_nulls()
    {
        var store = CreateStore();

        var loaded = await store.GetStateAsync(CancellationToken.None);

        Assert.Null(loaded.Position);
        Assert.Null(loaded.LastDailyMessageDateLocal);
        Assert.Null(loaded.LastEstado);
    }

    [Fact]
    public async Task Corrupt_file_returns_nulls_without_throwing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        await File.WriteAllTextAsync(_file, "{ this is not valid json ");
        var store = CreateStore();

        var loaded = await store.GetStateAsync(CancellationToken.None);

        Assert.Null(loaded.Position);
        Assert.Null(loaded.LastDailyMessageDateLocal);
        Assert.Null(loaded.LastEstado);
    }

    [Fact]
    public async Task Legacy_file_without_estado_loads_with_null_estado()
    {
        // A state file written before LastEstado existed must still load (field absent -> null).
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        await File.WriteAllTextAsync(_file,
            "{ \"Position\": 16, \"LastDailyMessageDateLocal\": \"2026-07-28\", \"UpdatedAtUtc\": \"2026-07-28T00:00:00+00:00\" }");
        var store = CreateStore();

        var loaded = await store.GetStateAsync(CancellationToken.None);

        Assert.Equal(16, loaded.Position);
        Assert.Equal(new DateOnly(2026, 7, 28), loaded.LastDailyMessageDateLocal);
        Assert.Null(loaded.LastEstado);
    }

    [Fact]
    public async Task Save_overwrites_previous_value()
    {
        var store = CreateStore();

        await store.SaveStateAsync(new WaitlistState(16, new DateOnly(2026, 7, 27), "En espera"), CancellationToken.None);
        await store.SaveStateAsync(new WaitlistState(14, new DateOnly(2026, 7, 28), "En espera"), CancellationToken.None);
        var loaded = await store.GetStateAsync(CancellationToken.None);

        Assert.Equal(14, loaded.Position);
        Assert.Equal(new DateOnly(2026, 7, 28), loaded.LastDailyMessageDateLocal);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best effort cleanup */ }
    }
}
