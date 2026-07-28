using UcamWaitlistBot.Configuration;
using UcamWaitlistBot.Models;
using UcamWaitlistBot.Workers;
using Xunit;

namespace UcamWaitlistBot.Tests;

public class ReportDeciderTests
{
    private static readonly DateOnly Today = new(2026, 7, 28);
    private static readonly DateOnly Yesterday = new(2026, 7, 27);
    private const string OnWaitlist = "En espera";

    // Defaults mirror WorkerOptions defaults (both notify toggles on).
    private static WorkerOptions Options(bool notifyOnStartup = true, bool sendDaily = true) =>
        new() { NotifyOnStartup = notifyOnStartup, SendDailyMorningMessage = sendDaily };

    private static ReportDecision Decide(
        WaitlistState previous, string estado, int? position,
        bool isFirstProcessCheck = false, bool notifyOnStartup = false, bool sendDaily = false) =>
        ReportDecider.Decide(previous, estado, position, isFirstProcessCheck, Today,
            Options(notifyOnStartup, sendDaily));

    [Fact]
    public void FirstRun_reports_initial()
    {
        var d = Decide(new WaitlistState(null, null, null), OnWaitlist, 16);

        Assert.True(d.ShouldReport);
        Assert.Equal(ReportReason.Initial, d.Reason);
    }

    [Fact]
    public void Changed_position_reports_position_changed()
    {
        var d = Decide(new WaitlistState(16, Today, OnWaitlist), OnWaitlist, 14);

        Assert.True(d.ShouldReport);
        Assert.Equal(ReportReason.PositionChanged, d.Reason);
    }

    [Fact]
    public void Unchanged_same_day_no_startup_does_not_report()
    {
        var d = Decide(new WaitlistState(16, Today, OnWaitlist), OnWaitlist, 16, isFirstProcessCheck: true);

        Assert.False(d.ShouldReport);
        Assert.Equal(ReportReason.None, d.Reason);
    }

    [Fact]
    public void Unchanged_new_day_reports_initial()
    {
        var d = Decide(new WaitlistState(16, Yesterday, OnWaitlist), OnWaitlist, 16, sendDaily: true);

        Assert.True(d.ShouldReport);
        Assert.Equal(ReportReason.Initial, d.Reason);
    }

    [Fact]
    public void DailyMessage_disabled_suppresses_new_day_report()
    {
        var d = Decide(new WaitlistState(16, Yesterday, OnWaitlist), OnWaitlist, 16, sendDaily: false);

        Assert.False(d.ShouldReport);
    }

    [Fact]
    public void StartupPing_reports_when_first_process_check()
    {
        var d = Decide(new WaitlistState(16, Today, OnWaitlist), OnWaitlist, 16,
            isFirstProcessCheck: true, notifyOnStartup: true, sendDaily: true);

        Assert.True(d.ShouldReport);
        Assert.Equal(ReportReason.Initial, d.Reason);
    }

    [Fact]
    public void StartupPing_only_on_first_process_check()
    {
        var d = Decide(new WaitlistState(16, Today, OnWaitlist), OnWaitlist, 16,
            isFirstProcessCheck: false, notifyOnStartup: true, sendDaily: true);

        Assert.False(d.ShouldReport);
    }

    [Fact]
    public void Leaving_waitlist_reports_admission()
    {
        var d = Decide(new WaitlistState(16, Today, OnWaitlist), "Admitido", null);

        Assert.True(d.ShouldReport);
        Assert.Equal(ReportReason.Admission, d.Reason);
    }

    [Fact]
    public void Admission_takes_priority_over_position_change()
    {
        // Position also "changed" (16 -> null is not a numeric change, but estado dominates regardless).
        var d = Decide(new WaitlistState(16, Today, OnWaitlist), "Matrícula abierta", 3);

        Assert.Equal(ReportReason.Admission, d.Reason);
    }

    [Fact]
    public void Other_estado_change_while_on_waitlist_reports_status_changed()
    {
        var d = Decide(new WaitlistState(16, Today, "En espera"), "En espera (revisión)", 16);

        Assert.True(d.ShouldReport);
        Assert.Equal(ReportReason.StatusChanged, d.Reason);
    }

    [Fact]
    public void Null_position_unchanged_estado_does_not_report()
    {
        // Already admitted previously (estado not on waitlist, no number); a later identical check is quiet.
        var d = Decide(new WaitlistState(null, Today, "Admitido"), "Admitido", null);

        Assert.False(d.ShouldReport);
        Assert.Equal(ReportReason.None, d.Reason);
    }

    [Fact]
    public void Estado_comparison_is_trim_and_case_insensitive()
    {
        var d = Decide(new WaitlistState(16, Today, "En espera"), "  en ESPERA ", 16);

        Assert.False(d.ShouldReport);
    }
}
