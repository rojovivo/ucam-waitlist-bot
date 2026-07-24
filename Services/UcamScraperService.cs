using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Polly;
using Polly.Registry;
using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;

namespace UcamWaitlistBot.Services;

/// <summary>
/// Playwright implementation of <see cref="IUcamScraperService"/>.
/// Walks the SPA: login → dashboard → results step → read the waitlist position.
/// </summary>
public sealed class UcamScraperService : IUcamScraperService
{
    /// <summary>Key used to resolve the shared Polly pipeline registered in <c>Program.cs</c>.</summary>
    public const string ResiliencePipelineKey = "ucam-scrape";

    private readonly IBrowserProvider _browserProvider;
    private readonly PortalOptions _portal;
    private readonly int _timeoutMilliseconds;
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<UcamScraperService> _logger;

    public UcamScraperService(
        IBrowserProvider browserProvider,
        IOptions<PortalOptions> portalOptions,
        IOptions<WorkerOptions> workerOptions,
        ResiliencePipelineProvider<string> pipelineProvider,
        ILogger<UcamScraperService> logger)
    {
        _browserProvider = browserProvider;
        _portal = portalOptions.Value;
        _timeoutMilliseconds = (int)workerOptions.Value.NavigationTimeout.TotalMilliseconds;
        _pipeline = pipelineProvider.GetPipeline(ResiliencePipelineKey);
        _logger = logger;
    }

    public async Task<WaitlistResult> GetWaitlistPositionAsync(CancellationToken cancellationToken)
    {
        // The whole browser flow is retried as a unit: a transient timeout early in the flow
        // invalidates every later step, so re-running from a clean context is the safe recovery.
        return await _pipeline
            .ExecuteAsync(async token => await ScrapeOnceAsync(token).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<WaitlistResult> ScrapeOnceAsync(CancellationToken cancellationToken)
    {
        var browser = await _browserProvider.GetBrowserAsync().ConfigureAwait(false);

        // A fresh context per attempt guarantees no session/cookie bleed between checks.
        await using var context = await browser.NewContextAsync().ConfigureAwait(false);
        context.SetDefaultTimeout(_timeoutMilliseconds);

        var page = await context.NewPageAsync().ConfigureAwait(false);

        await LoginAsync(page, cancellationToken).ConfigureAwait(false);
        await OpenProgramAsync(page, cancellationToken).ConfigureAwait(false);
        await NavigateToResultStepAsync(page, cancellationToken).ConfigureAwait(false);
        var position = await ReadWaitlistPositionAsync(page, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Read waitlist position {Position} for '{Program}'.", position, _portal.ProgramName);
        return new WaitlistResult(_portal.ProgramName, position, DateTimeOffset.UtcNow);
    }

    private async Task LoginAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Navigating to login page.");

        await page.GotoAsync(_portal.LoginUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle })
            .ConfigureAwait(false);

        // The form is rendered client-side by Aura; wait for the username input before filling.
        var usernameInput = page.Locator(PortalSelectors.UsernameInput);
        await usernameInput.WaitForAsync().ConfigureAwait(false);

        await usernameInput.FillAsync(_portal.Username).ConfigureAwait(false);
        await page.Locator(PortalSelectors.PasswordInput).FillAsync(_portal.Password).ConfigureAwait(false);

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = PortalSelectors.LoginButtonName })
            .ClickAsync()
            .ConfigureAwait(false);

        // Login triggers a full SPA navigation to the authenticated dashboard; wait for it to settle.
        await page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase))
            .ConfigureAwait(false);
    }

    private async Task OpenProgramAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Waiting for dashboard and opening program '{Program}'.", _portal.ProgramName);

        // The dashboard is LWC <div>s (not a table). The active in-process admission exposes a single
        // "Continuar" button, so we click it directly.
        var continueButton = page
            .GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = PortalSelectors.ContinueButtonName });

        await continueButton.First.WaitForAsync().ConfigureAwait(false);
        await continueButton.First.ClickAsync().ConfigureAwait(false);

        // Verify the wizard opened for the expected program (the header shows the titulación).
        // Guards against clicking through to the wrong program if the dashboard ever changes.
        await page.GetByText(_portal.ProgramName).First
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible })
            .ConfigureAwait(false);
    }

    private async Task NavigateToResultStepAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Navigating to the results step (Paso 4 de 6).");

        // "Consulta el resultado" is a clickable item in the 6-step progress bar.
        await page.GetByText(PortalSelectors.ResultStepText).First.ClickAsync().ConfigureAwait(false);
    }

    private async Task<int> ReadWaitlistPositionAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Reading the waitlist position from the results grid.");

        // The results block is two sibling LWC grids: a header row of label cells and a value row of
        // data cells, aligned by column index. Find the index of the "Posición de espera" header,
        // then read the value cell at the same index.
        var headerCells = page.Locator(PortalSelectors.ResultHeaderCells);
        var valueCells = page.Locator(PortalSelectors.ResultValueCells);

        await headerCells.First.WaitForAsync().ConfigureAwait(false);

        var headerRegex = new Regex(PortalSelectors.WaitlistHeaderPattern, RegexOptions.IgnoreCase);
        var headerCount = await headerCells.CountAsync().ConfigureAwait(false);

        var targetIndex = -1;
        for (var i = 0; i < headerCount; i++)
        {
            var headerText = (await headerCells.Nth(i).InnerTextAsync().ConfigureAwait(false)).Trim();
            if (headerRegex.IsMatch(headerText))
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0)
        {
            throw new InvalidOperationException(
                $"Could not find a results column matching '{PortalSelectors.WaitlistHeaderPattern}'.");
        }

        var valueCell = valueCells.Nth(targetIndex);
        await valueCell.WaitForAsync().ConfigureAwait(false);
        var rawValue = (await valueCell.InnerTextAsync().ConfigureAwait(false)).Trim();

        if (!int.TryParse(rawValue, out var position))
        {
            throw new InvalidOperationException(
                $"Could not parse waitlist position from cell text '{rawValue}'.");
        }

        return position;
    }
}
