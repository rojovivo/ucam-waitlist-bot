# UCAM Waitlist Tracking Bot

A .NET 10 Worker Service that logs into the UCAM admissions portal (a Salesforce SPA)
with Playwright every ~15 minutes, reads the **POSICIÓN DE ESPERA** for a program, and
sends a Telegram message whenever it changes or on the first run. Scrape failures raise a
Telegram alert and the loop keeps running. The last position is persisted to a JSON file
so restarts do not produce duplicate notifications.

## Architecture

| Concern            | Type                                             |
|--------------------|--------------------------------------------------|
| Scheduling         | `Workers/WaitlistWorker.cs` (`BackgroundService` + `PeriodicTimer`) |
| Scraping           | `IUcamScraperService` → `UcamScraperService`     |
| Browser lifetime   | `IBrowserProvider` → `PlaywrightBrowserProvider` |
| Notifications      | `ITelegramNotifier` → `TelegramNotifier`         |
| State persistence  | `IPositionStore` → `JsonPositionStore`           |
| Selectors (tuning) | `Services/PortalSelectors.cs`                    |
| Config             | `Configuration/*Options.cs` (IOptions + validation) |
| Resilience         | Polly retry pipeline registered in `Program.cs`  |

## Setup

```bash
dotnet restore
dotnet build

# Install the Chromium binary Playwright drives (run once).
pwsh bin/Debug/net10.0/playwright.ps1 install chromium
```

### Secrets (do not commit)

```bash
dotnet user-secrets set "Portal:Username"  "you@example.com"
dotnet user-secrets set "Portal:Password"  "••••••••"
dotnet user-secrets set "Telegram:BotToken" "123456:ABC-DEF..."
dotnet user-secrets set "Telegram:ChatId"   "123456789"
```

User-secrets are loaded in every environment (see `Program.cs`), so the deployed exe
running as your user picks them up without extra configuration. Alternatively, provide the
same values as environment variables, e.g. `Portal__Username`, `Telegram__BotToken`
(double underscore = section separator).

## Running

```bash
dotnet run
```

First run locally with `Worker:Headless=false` (already the default in
`appsettings.Development.json`) to watch the browser and confirm the selectors in
`PortalSelectors.cs` match the live DOM. Adjust that one file if any step cannot find its
element. Set `Headless` back to `true` for deployment.

## Run continuously (Windows, at logon)

Publish, then register a scheduled task that starts the bot hidden when you log on and
auto-restarts it on failure:

```powershell
dotnet publish UcamWaitlistBot.csproj -c Release -o "C:\Users\<you>\Apps\UcamWaitlistBot"

$dir = "C:\Users\<you>\Apps\UcamWaitlistBot"
$action  = New-ScheduledTaskAction -Execute "wscript.exe" -Argument "`"$dir\start-hidden.vbs`"" -WorkingDirectory $dir
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
              -StartWhenAvailable -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) `
              -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName "UCAM Waitlist Bot" -Action $action -Trigger $trigger -Settings $settings
```

`start-hidden.vbs` (in the publish folder) launches the exe with no console window. The task
runs in your user session so user-secrets are available. Manage it with
`Start-ScheduledTask` / `Stop-ScheduledTask` / `Unregister-ScheduledTask -TaskName "UCAM Waitlist Bot"`.
Set `Worker:StateFilePath` to an absolute path so state is written regardless of working
directory.

## Notifications

- **Change:** whenever the waitlist position differs from the last stored value.
- **Daily "morning" message:** once per calendar day on the first check inside the active
  window (`Worker:SendDailyMorningMessage`, default `true`).
- **Startup ping:** on the first check after each process start (`Worker:NotifyOnStartup`,
  default `true`).
- **Error:** if a check fails after retries.

## Configuration reference (`appsettings.json`)

| Key                      | Meaning                                             |
|--------------------------|-----------------------------------------------------|
| `Portal:LoginUrl`        | SPA login URL                                       |
| `Portal:ProgramName`     | Row to open, e.g. `Grado en Fisioterapia`           |
| `Worker:PollInterval`    | Base interval between checks (`01:00:00`)           |
| `Worker:ActiveHoursStart`/`ActiveHoursEnd` | Daily active window, local time (`08:00:00`–`20:00:00`) |
| `Worker:MaxJitter`       | Random delay added before each check (`00:02:00`)   |
| `Worker:StateFilePath`   | JSON state file (absolute path recommended)         |
| `Worker:Headless`        | Run Chromium headless                               |
| `Worker:NotifyOnStartup` | Send current position on first check after start    |
| `Worker:SendDailyMorningMessage` | Send current position once per day (morning) |
| `Worker:NavigationTimeout` | Per-action Playwright timeout                     |

## Notes

- Selectors are best-effort semantic locators; the results step tab and table are the
  expected tuning points on the first headed run.
- Assumes plain username/password login. MFA/captcha would require reworking `LoginAsync`.
