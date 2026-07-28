using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;
using UcamWaitlistBot.Workers;
using Xunit;

namespace UcamWaitlistBot.Tests;

public class ReportDeciderTests
{
    private static readonly DateOnly Today = new(2026, 7, 28);
    private static readonly DateOnly Yesterday = new(2026, 7, 27);

    // Defaults mirror WorkerOptions defaults (both notify toggles on).
    private static WorkerOptions Options(bool notifyOnStartup = true, bool sendDaily = true) =>
        new() { NotifyOnStartup = notifyOnStartup, SendDailyMorningMessage = sendDaily };

    [Fact]
    public void FirstRun_reports_as_initial()
    {
        var decision = ReportDecider.Decide(
            previous: new WaitlistState(null, null),
            currentPosition: 16,
            isFirstProcessCheck: false,
            Today,
            Options(notifyOnStartup: false, sendDaily: false));

        Assert.True(decision.ShouldReport);
        Assert.False(decision.ReportAsChange);
    }

    [Fact]
    public void Changed_position_reports_as_change()
    {
        var decision = ReportDecider.Decide(
            previous: new WaitlistState(16, Today),
            currentPosition: 14,
            isFirstProcessCheck: false,
            Today,
            Options(notifyOnStartup: false, sendDaily: false));

        Assert.True(decision.ShouldReport);
        Assert.True(decision.ReportAsChange);
    }

    [Fact]
    public void Unchanged_same_day_no_startup_does_not_report()
    {
        var decision = ReportDecider.Decide(
            previous: new WaitlistState(16, Today),
            currentPosition: 16,
            isFirstProcessCheck: true, // startup ping is OFF below, so this must not matter
            Today,
            Options(notifyOnStartup: false, sendDaily: true));

        Assert.False(decision.ShouldReport);
        Assert.False(decision.ReportAsChange);
    }

    [Fact]
    public void Unchanged_new_day_reports_daily_as_initial()
    {
        var decision = ReportDecider.Decide(
            previous: new WaitlistState(16, Yesterday),
            currentPosition: 16,
            isFirstProcessCheck: false,
            Today,
            Options(notifyOnStartup: false, sendDaily: true));

        Assert.True(decision.ShouldReport);
        Assert.False(decision.ReportAsChange);
    }

    [Fact]
    public void DailyMessage_disabled_suppresses_new_day_report()
    {
        var decision = ReportDecider.Decide(
            previous: new WaitlistState(16, Yesterday),
            currentPosition: 16,
            isFirstProcessCheck: false,
            Today,
            Options(notifyOnStartup: false, sendDaily: false));

        Assert.False(decision.ShouldReport);
    }

    [Fact]
    public void StartupPing_reports_when_first_process_check_even_if_unchanged_and_messaged_today()
    {
        var decision = ReportDecider.Decide(
            previous: new WaitlistState(16, Today),
            currentPosition: 16,
            isFirstProcessCheck: true,
            Today,
            Options(notifyOnStartup: true, sendDaily: true));

        Assert.True(decision.ShouldReport);
        Assert.False(decision.ReportAsChange);
    }

    [Fact]
    public void StartupPing_only_on_first_process_check()
    {
        var decision = ReportDecider.Decide(
            previous: new WaitlistState(16, Today),
            currentPosition: 16,
            isFirstProcessCheck: false,
            Today,
            Options(notifyOnStartup: true, sendDaily: true));

        Assert.False(decision.ShouldReport);
    }
}
