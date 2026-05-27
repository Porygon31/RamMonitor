using FluentAssertions;
using RamMonitor.Core.Models;
using RamMonitor.Core.Services;
using Xunit;

namespace RamMonitor.Tests;

/// <summary>
/// Tests de <see cref="AlertEvaluator"/>.
/// </summary>
public sealed class AlertEvaluatorTests
{
    private static MemorySnapshot BuildMemory(double usedPercent, double pagefilePercent = 0)
    {
        ulong total = 16UL * 1024 * 1024 * 1024;
        ulong used = (ulong)(total * (usedPercent / 100.0));
        ulong avail = total - used;

        ulong pfTotal = 32UL * 1024 * 1024 * 1024;
        ulong pfUsed = (ulong)(pfTotal * (pagefilePercent / 100.0));
        ulong pfAvail = pfTotal - pfUsed;

        return new MemorySnapshot
        {
            Timestamp = DateTimeOffset.Now,
            TotalPhysicalBytes = total,
            AvailablePhysicalBytes = avail,
            UsedPhysicalBytes = used,
            CommitTotalBytes = used,
            CommitLimitBytes = total,
            PageFileTotalBytes = pfTotal,
            PageFileAvailableBytes = pfAvail,
            KernelPagedBytes = 0,
            KernelNonPagedBytes = 0,
            SystemCacheBytes = 0,
            StandbyCacheBytes = 0,
            ModifiedPageListBytes = 0,
            FreeAndZeroPageListBytes = 0,
            HardwareReservedBytes = 0
        };
    }

    private static ProcessSnapshot BuildProcess(int pid, string name, ulong workingSetBytes)
        => new()
        {
            Timestamp = DateTimeOffset.Now,
            ProcessId = pid,
            Name = name,
            WorkingSetBytes = workingSetBytes,
            PrivateBytes = workingSetBytes,
            VirtualSizeBytes = workingSetBytes,
            PagedPoolBytes = 0,
            NonPagedPoolBytes = 0,
            PagefileBytes = 0,
            HandleCount = 0,
            ThreadCount = 0
        };

    [Fact]
    public void Evaluate_GlobalRamAboveThreshold_RaisesWarning()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "ram.warn",
                DisplayName = "warn",
                Kind = AlertRuleKind.GlobalRamPercent,
                Threshold = 80.0,
                Severity = AlertSeverity.Warning
            }
        });

        var memory = BuildMemory(usedPercent: 85.0);

        var alerts = evaluator.Evaluate(memory, Array.Empty<ProcessSnapshot>(), DateTimeOffset.Now);

        alerts.Should().ContainSingle();
        alerts[0].Kind.Should().Be(AlertRuleKind.GlobalRamPercent);
        alerts[0].Severity.Should().Be(AlertSeverity.Warning);
        alerts[0].MeasuredValue.Should().BeApproximately(85.0, 0.5);
    }

    [Fact]
    public void Evaluate_GlobalRamBelowThreshold_NoAlert()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "ram.warn", DisplayName = "warn",
                Kind = AlertRuleKind.GlobalRamPercent,
                Threshold = 80.0, Severity = AlertSeverity.Warning
            }
        });

        var memory = BuildMemory(usedPercent: 50.0);

        evaluator.Evaluate(memory, Array.Empty<ProcessSnapshot>(), DateTimeOffset.Now)
            .Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_DisabledRule_NoAlert()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "ram.warn", DisplayName = "warn",
                Kind = AlertRuleKind.GlobalRamPercent,
                Threshold = 80.0, Severity = AlertSeverity.Warning,
                Enabled = false
            }
        });

        var memory = BuildMemory(usedPercent: 95.0);

        evaluator.Evaluate(memory, Array.Empty<ProcessSnapshot>(), DateTimeOffset.Now)
            .Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_TwoConsecutiveTriggersWithinMinInterval_OnlyFiresOnce()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "ram.warn", DisplayName = "warn",
                Kind = AlertRuleKind.GlobalRamPercent,
                Threshold = 80.0, Severity = AlertSeverity.Warning,
                MinReNotifyInterval = TimeSpan.FromMinutes(5)
            }
        });

        var memory = BuildMemory(usedPercent: 90.0);
        var t0 = DateTimeOffset.Now;

        var first = evaluator.Evaluate(memory, Array.Empty<ProcessSnapshot>(), t0);
        var second = evaluator.Evaluate(memory, Array.Empty<ProcessSnapshot>(), t0.AddSeconds(1));

        first.Should().ContainSingle();
        second.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_TwoConsecutiveTriggersAfterMinInterval_FiresTwice()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "ram.warn", DisplayName = "warn",
                Kind = AlertRuleKind.GlobalRamPercent,
                Threshold = 80.0, Severity = AlertSeverity.Warning,
                MinReNotifyInterval = TimeSpan.FromMinutes(5)
            }
        });

        var memory = BuildMemory(usedPercent: 90.0);
        var t0 = DateTimeOffset.Now;

        var first = evaluator.Evaluate(memory, Array.Empty<ProcessSnapshot>(), t0);
        var second = evaluator.Evaluate(memory, Array.Empty<ProcessSnapshot>(), t0.AddMinutes(6));

        first.Should().ContainSingle();
        second.Should().ContainSingle();
    }

    [Fact]
    public void Evaluate_ProcessWorkingSetExceedsThreshold_RaisesPerProcessAlert()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "proc.ws", DisplayName = "ws",
                Kind = AlertRuleKind.ProcessWorkingSetMb,
                Threshold = 1024.0,
                Severity = AlertSeverity.Warning
            }
        });

        var processes = new[]
        {
            BuildProcess(100, "small", 100UL * 1024 * 1024),
            BuildProcess(200, "big",   2000UL * 1024 * 1024)
        };

        var alerts = evaluator.Evaluate(BuildMemory(50.0), processes, DateTimeOffset.Now);

        alerts.Should().ContainSingle();
        alerts[0].RelatedProcessId.Should().Be(200);
        alerts[0].RelatedProcessName.Should().Be("big");
    }

    [Fact]
    public void Evaluate_TwoProcessesAboveThreshold_RaisesTwoAlertsAtSameTick()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "proc.ws", DisplayName = "ws",
                Kind = AlertRuleKind.ProcessWorkingSetMb,
                Threshold = 500.0,
                Severity = AlertSeverity.Warning
            }
        });

        var processes = new[]
        {
            BuildProcess(100, "a", 1024UL * 1024 * 1024),
            BuildProcess(200, "b", 1024UL * 1024 * 1024)
        };

        var alerts = evaluator.Evaluate(BuildMemory(50.0), processes, DateTimeOffset.Now);

        alerts.Should().HaveCount(2);
        alerts.Select(a => a.RelatedProcessId).Should().BeEquivalentTo(new int?[] { 100, 200 });
    }

    [Fact]
    public void EvaluateLeak_NotSuspected_ReturnsEmpty()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "leak.detected", DisplayName = "leak",
                Kind = AlertRuleKind.LeakDetected,
                Threshold = 0.0, Severity = AlertSeverity.Warning
            }
        });

        var leak = new LeakReport
        {
            ProcessId = 1, ProcessName = "x",
            WindowStart = DateTimeOffset.Now.AddMinutes(-10),
            WindowEnd = DateTimeOffset.Now,
            SampleCount = 30, GrowthRateBytesPerMinute = 100,
            RSquared = 0.99,
            StartWorkingSetBytes = 1000, EndWorkingSetBytes = 2000,
            IsSuspectedLeak = false
        };

        evaluator.EvaluateLeak(leak, DateTimeOffset.Now).Should().BeEmpty();
    }

    [Fact]
    public void EvaluateLeak_Suspected_RaisesAlertWithProcessInfo()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "leak.detected", DisplayName = "leak",
                Kind = AlertRuleKind.LeakDetected,
                Threshold = 0.0, Severity = AlertSeverity.Warning,
                MinReNotifyInterval = TimeSpan.FromMinutes(15)
            }
        });

        var leak = new LeakReport
        {
            ProcessId = 42,
            ProcessName = "leaker",
            WindowStart = DateTimeOffset.Now.AddMinutes(-30),
            WindowEnd = DateTimeOffset.Now,
            SampleCount = 30,
            GrowthRateBytesPerMinute = 5 * 1024 * 1024,
            RSquared = 0.95,
            StartWorkingSetBytes = 100_000_000,
            EndWorkingSetBytes = 250_000_000,
            IsSuspectedLeak = true
        };

        var alerts = evaluator.EvaluateLeak(leak, DateTimeOffset.Now);

        alerts.Should().ContainSingle();
        alerts[0].Kind.Should().Be(AlertRuleKind.LeakDetected);
        alerts[0].RelatedProcessId.Should().Be(42);
        alerts[0].RelatedProcessName.Should().Be("leaker");
        alerts[0].MeasuredValue.Should().BeApproximately(5 * 1024 * 1024, 1.0);
    }

    [Fact]
    public void EvaluateLeak_AntiSpamScoped_PerProcess()
    {
        var evaluator = new AlertEvaluator();
        evaluator.SetRules(new[]
        {
            new AlertRule
            {
                Id = "leak.detected", DisplayName = "leak",
                Kind = AlertRuleKind.LeakDetected,
                Threshold = 0.0, Severity = AlertSeverity.Warning,
                MinReNotifyInterval = TimeSpan.FromMinutes(15)
            }
        });

        LeakReport BuildLeak(int pid, string name) => new()
        {
            ProcessId = pid, ProcessName = name,
            WindowStart = DateTimeOffset.Now.AddMinutes(-30),
            WindowEnd = DateTimeOffset.Now,
            SampleCount = 30, GrowthRateBytesPerMinute = 5 * 1024 * 1024,
            RSquared = 0.95,
            StartWorkingSetBytes = 100_000_000, EndWorkingSetBytes = 250_000_000,
            IsSuspectedLeak = true
        };

        var t = DateTimeOffset.Now;
        var alertsA = evaluator.EvaluateLeak(BuildLeak(1, "a"), t);
        var alertsB = evaluator.EvaluateLeak(BuildLeak(2, "b"), t);

        alertsA.Should().ContainSingle();
        alertsB.Should().ContainSingle();
    }
}
