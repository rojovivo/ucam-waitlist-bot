# UCAM Waitlist Tracking Bot

A .NET 10 worker that logs into the UCAM admissions portal (a Salesforce/LWC SPA) with
Playwright, reads the **ESTADO** and **POSICIÓN DE ESPERA** for a program, and sends a Telegram
message when something noteworthy happens. It runs as a **one-shot job in GitHub Actions**,
triggered on a schedule by **cron-job.org** — there is no always-on server. State is committed
back to the repo so each stateless run knows what the previous one saw.

## How it runs (hosting)

- **Execution:** GitHub Actions workflow [`.github/workflows/check-waitlist.yml`](.github/workflows/check-waitlist.yml).
  Each run does a single check and exits (`Worker:RunOnce=true`).
- **Scheduling:** external — **cron-job.org** calls the GitHub `workflow_dispatch` API on a
  schedule (see [Scheduling](#scheduling)). GitHub's built-in `schedule` cron was removed because
  it delayed/dropped runs.
- **Cross-run state:** the run commits `data/last-position.json` back to `main` (`[skip ci]`), so
  the next run can detect changes and fire the once-per-day message.
- **Secrets:** GitHub Actions repository secrets (below). Nothing runs locally against the live
  portal.

## Architecture

| Concern | Type |
|---|---|
| Orchestration (one check) | `Workers/WaitlistWorker.cs` (`BackgroundService`, run-once mode) |
| Report decision (pure)     | `Workers/ReportDecider.cs` → `ReportReason` |
| Scraping                   | `IUcamScraperService` → `UcamScraperService` |
| Browser lifetime           | `IBrowserProvider` → `PlaywrightBrowserProvider` |
| Notifications              | `ITelegramNotifier` → `TelegramNotifier` (+ `TelegramMessageFormatter`) |
| State persistence          | `IPositionStore` → `JsonPositionStore` |
| Selectors (tuning surface) | `Services/PortalSelectors.cs` |
| Config                     | `Configuration/*Options.cs` (IOptions + validation) |
| Resilience                 | Polly retry pipeline in `Program.cs` |
| Tests / CI                 | `tests/UcamWaitlistBot.Tests` (xUnit) + `.github/workflows/ci.yml` |

## Notifications

- **Admission:** when `ESTADO` leaves "En espera" (a likely admission) — a 🎉 message; tolerates a
  now-empty/non-numeric position.
- **Status changed:** any other change to the `ESTADO` text.
- **Position changed:** the waitlist number changed.
- **Daily "morning" message:** once per calendar day on the first in-window check
  (`Worker:SendDailyMorningMessage`, default `true`).
- **Error:** if a check fails after retries.

(`Worker:NotifyOnStartup` — send on every process start — exists but is set **`false`** in CI, so
hourly runs don't spam; it's only useful for a long-running/local process.)

## Scheduling

Configured in **cron-job.org**, timezone **Europe/Madrid**, calling
`POST https://api.github.com/repos/rojovivo/ucam-waitlist-bot/actions/workflows/check-waitlist.yml/dispatches`
with body `{"ref":"main"}` and a fine-grained PAT (**Actions: Read and write**, this repo only).

Current cron expression (summer admissions window):

```
0 8-18 * 7-9 1-5
```
→ on the hour, 08:00–18:00, **July–September only**, Monday–Friday, Madrid time.

The in-app active-hours gate (`ActiveHoursStart`/`End`, default 08:00–20:00) plus `TZ=Europe/Madrid`
remain as a safety net: any trigger outside the window self-skips. Weekend/season exclusion lives
only in the cron-job.org schedule.

## Secrets (GitHub Actions → Settings → Secrets → Actions)

| Secret | Purpose |
|---|---|
| `PORTAL_USERNAME` / `PORTAL_PASSWORD` | Portal login |
| `TELEGRAM_BOTTOKEN` / `TELEGRAM_CHATID` | Bot token + destination chat |

The workflow maps them to config via env vars (`Portal__Username`, `Telegram__BotToken`, …).
`appsettings.json` stays blanked (template only); user-secrets also load locally for tests.

## Configuration reference (`appsettings.json` / env overrides)

| Key | Meaning |
|---|---|
| `Portal:LoginUrl` | SPA login URL |
| `Portal:ProgramName` | Program to open, e.g. `Grado en Fisioterapia` |
| `Worker:RunOnce` | Do a single check and exit (CI sets `true`) |
| `Worker:ActiveHoursStart` / `ActiveHoursEnd` | Daily active window, local time (safety net) |
| `Worker:StateFilePath` | State JSON path (CI: `data/last-position.json`) |
| `Worker:Headless` | Run Chromium headless (`true`) |
| `Worker:NotifyOnStartup` | Report on first check per process (CI: `false`) |
| `Worker:SendDailyMorningMessage` | Report once per day on first in-window check |
| `Worker:NavigationTimeout` | Per-action Playwright timeout |
| `Worker:PollInterval` / `MaxJitter` | Loop interval / jitter — only used in continuous (non-`RunOnce`) mode |

## Development

The unit tests are pure (no browser, no network), so they run anywhere:

```bash
dotnet test          # build + 27 tests
dotnet build -c Release
```

CI ([`ci.yml`](.github/workflows/ci.yml)) runs the same on every push/PR. The app is **not** run
locally against the live portal (network access to UCAM/Telegram is restricted); all live behaviour
is exercised via GitHub Actions (`workflow_dispatch`).

## Notes

- Selectors in `PortalSelectors.cs` were confirmed against the live DOM; they're the single place to
  adjust if the portal markup changes. A broken scrape fails loudly (error alert + red run).
- The admitted-state DOM couldn't be observed, so admission is detected defensively (ESTADO leaving
  "En espera"), covered by unit tests.
- Assumes plain username/password login; MFA/captcha would require reworking `LoginAsync`.
