using Microsoft.Playwright;
using Polly;
using Telegram.Bot;
using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Services;
using UcamWaitlistBot.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Load user-secrets in every environment (not just Development). The app is deployed to run as
// the current user via Task Scheduler in the Production environment, where secrets would not
// otherwise be loaded. Safe here: single-user personal machine, running under that user's account.
builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);

// --- Configuration (IOptions pattern, validated at startup) ---
builder.Services
    .AddOptions<PortalOptions>()
    .BindConfiguration(PortalOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<TelegramOptions>()
    .BindConfiguration(TelegramOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<WorkerOptions>()
    .BindConfiguration(WorkerOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- Telegram bot client (singleton, one HTTP client for the process) ---
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramOptions>>().Value;
    return new TelegramBotClient(options.BotToken);
});

// --- Playwright browser lifetime (singleton, launched once, disposed on shutdown) ---
builder.Services.AddSingleton<IBrowserProvider, PlaywrightBrowserProvider>();

// --- Application services ---
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddScoped<IPositionStore, JsonPositionStore>();
builder.Services.AddScoped<IUcamScraperService, UcamScraperService>();

// --- Polly resilience: retry the scrape on transient Playwright timeouts / errors ---
builder.Services.AddResiliencePipeline(UcamScraperService.ResiliencePipelineKey, pipeline =>
{
    pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(5),
        ShouldHandle = new PredicateBuilder()
            .Handle<PlaywrightException>()
            .Handle<TimeoutException>()
    });
});

// --- Background worker ---
builder.Services.AddHostedService<WaitlistWorker>();

var host = builder.Build();
await host.RunAsync();
