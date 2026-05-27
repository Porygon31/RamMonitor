using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RamMonitor.Core.Models;
using RamMonitor.Core.Services;
using Xunit;

namespace RamMonitor.Tests;

/// <summary>
/// Tests de <see cref="LeakDetector"/>.
/// </summary>
public sealed class LeakDetectorTests
{
    private static LeakDetector CreateDetector(double growthThresholdBytesPerMin = 1_048_576,
        double minR2 = 0.85, int minSamples = 20, int windowMinutes = 30)
    {
        var options = new LeakDetectorOptions
        {
            Window = TimeSpan.FromMinutes(windowMinutes),
            MinSamples = minSamples,
            GrowthRateBytesPerMinuteThreshold = growthThresholdBytesPerMin,
            MinR2 = minR2
        };

        return new LeakDetector(options, NullLogger<LeakDetector>.Instance);
    }

    private static IReadOnlyList<ProcessSnapshot> GenerateLinearSeries(int count, ulong startBytes,
        double growthBytesPerMinute, TimeSpan interval)
    {
        var now = DateTimeOffset.Now;
        var list = new List<ProcessSnapshot>(count);
        for (var i = 0; i < count; i++)
        {
            var ts = now.AddMinutes(-((count - 1 - i) * interval.TotalMinutes));
            var ws = (ulong)(startBytes + growthBytesPerMinute * i * interval.TotalMinutes);
            list.Add(new ProcessSnapshot
            {
                Timestamp = ts,
                ProcessId = 1234,
                Name = "leaker",
                WorkingSetBytes = ws,
                PrivateBytes = ws,
                VirtualSizeBytes = ws * 2,
                PagedPoolBytes = 0,
                NonPagedPoolBytes = 0,
                PagefileBytes = 0,
                HandleCount = 100,
                ThreadCount = 4
            });
        }
        return list;
    }

    [Fact]
    public void Analyze_NullSamples_ReturnsNull()
    {
        var detector = CreateDetector();

        detector.Analyze(1, "x", null!).Should().BeNull();
    }

    [Fact]
    public void Analyze_BelowMinSamples_ReturnsNull()
    {
        var detector = CreateDetector(minSamples: 20);
        var series = GenerateLinearSeries(5, 100_000_000, 5_000_000, TimeSpan.FromSeconds(1));

        detector.Analyze(1, "x", series).Should().BeNull();
    }

    [Fact]
    public void Analyze_StrongLinearGrowth_DetectsLeak()
    {
        var detector = CreateDetector();
        var series = GenerateLinearSeries(
            count: 30,
            startBytes: 100UL * 1024 * 1024,
            growthBytesPerMinute: 10.0 * 1024 * 1024,
            interval: TimeSpan.FromMinutes(1));

        var report = detector.Analyze(1234, "leaker", series);

        report.Should().NotBeNull();
        report!.IsSuspectedLeak.Should().BeTrue();
        report.SampleCount.Should().Be(30);
        report.RSquared.Should().BeApproximately(1.0, 1e-6);
        report.GrowthRateBytesPerMinute.Should().BeGreaterThan(9.0 * 1024 * 1024);
    }

    [Fact]
    public void Analyze_StableMemory_DoesNotFlagLeak()
    {
        var detector = CreateDetector();
        var series = GenerateLinearSeries(
            count: 30,
            startBytes: 200UL * 1024 * 1024,
            growthBytesPerMinute: 0.0,
            interval: TimeSpan.FromMinutes(1));

        var report = detector.Analyze(1234, "stable", series);

        report.Should().NotBeNull();
        report!.IsSuspectedLeak.Should().BeFalse();
    }

    [Fact]
    public void Analyze_SmallGrowthBelowThreshold_DoesNotFlagLeak()
    {
        var detector = CreateDetector(growthThresholdBytesPerMin: 1_048_576);
        var series = GenerateLinearSeries(30, 100_000_000, 0.5 * 1024 * 1024, TimeSpan.FromMinutes(1));

        var report = detector.Analyze(1, "minor-grow", series);

        report!.IsSuspectedLeak.Should().BeFalse();
    }

    [Fact]
    public void Analyze_NoisySeries_HasLowR2AndDoesNotFlagLeak()
    {
        var detector = CreateDetector(minR2: 0.85);
        var now = DateTimeOffset.Now;
        var rnd = new Random(42);
        var samples = new List<ProcessSnapshot>(50);

        for (var i = 0; i < 50; i++)
        {
            samples.Add(new ProcessSnapshot
            {
                Timestamp = now.AddMinutes(-(49 - i)),
                ProcessId = 99,
                Name = "noisy",
                WorkingSetBytes = (ulong)(150_000_000 + rnd.Next(-50_000_000, 50_000_000)),
                PrivateBytes = 0, VirtualSizeBytes = 0, PagedPoolBytes = 0, NonPagedPoolBytes = 0,
                PagefileBytes = 0, HandleCount = 0, ThreadCount = 0
            });
        }

        var report = detector.Analyze(99, "noisy", samples);

        report.Should().NotBeNull();
        report!.IsSuspectedLeak.Should().BeFalse();
        report.RSquared.Should().BeLessThan(0.5);
    }

    [Fact]
    public void AnalyzeAll_OnlyReturnsReportsForAnalyzableSeries()
    {
        var detector = CreateDetector();
        var goodSeries = GenerateLinearSeries(30, 100_000_000, 5 * 1024 * 1024, TimeSpan.FromMinutes(1));
        var shortSeries = GenerateLinearSeries(5, 100_000_000, 5 * 1024 * 1024, TimeSpan.FromMinutes(1));

        var samples = new Dictionary<int, IReadOnlyList<ProcessSnapshot>>
        {
            [1] = goodSeries,
            [2] = shortSeries
        };
        var names = new Dictionary<int, string>
        {
            [1] = "good",
            [2] = "short"
        };

        var reports = detector.AnalyzeAll(samples, names);

        reports.Should().ContainSingle(r => r.ProcessId == 1);
        reports.Should().NotContain(r => r.ProcessId == 2);
    }
}
