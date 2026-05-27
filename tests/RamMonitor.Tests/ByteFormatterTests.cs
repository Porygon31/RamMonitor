using System.Globalization;
using FluentAssertions;
using RamMonitor.Core.Utils;
using Xunit;

namespace RamMonitor.Tests;

/// <summary>
/// Tests de <see cref="ByteFormatter"/>.
/// </summary>
public sealed class ByteFormatterTests
{
    public ByteFormatterTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Theory]
    [InlineData(0UL, "0 B")]
    [InlineData(512UL, "512 B")]
    [InlineData(1024UL, "1 KB")]
    [InlineData(1536UL, "1.5 KB")]
    [InlineData(1048576UL, "1 MB")]
    [InlineData(1073741824UL, "1 GB")]
    [InlineData(1099511627776UL, "1 TB")]
    [InlineData(1125899906842624UL, "1 PB")]
    public void Format_KnownPowers_FormatAsExpected(ulong bytes, string expected)
    {
        ByteFormatter.Format(bytes).Should().Be(expected);
    }

    [Fact]
    public void Format_FractionalGB_RoundedToTwoDecimals()
    {
        ulong value = (ulong)(1.234 * 1024 * 1024 * 1024);

        ByteFormatter.Format(value).Should().Be("1.23 GB");
    }

    [Fact]
    public void Format_LongSignedNegative_KeepsMinusSign()
    {
        ByteFormatter.Format(-1048576L).Should().Be("-1 MB");
    }

    [Theory]
    [InlineData(0.0, "0 B/min")]
    [InlineData(1024.0, "1 KB/min")]
    [InlineData(1048576.0, "1 MB/min")]
    [InlineData(-1048576.0, "-1 MB/min")]
    public void FormatRatePerMinute_ValuesAreLabeledCorrectly(double bpm, string expected)
    {
        ByteFormatter.FormatRatePerMinute(bpm).Should().Be(expected);
    }

    [Fact]
    public void Format_RespectsDecimalsParameter()
    {
        ulong value = (ulong)(1.234 * 1024 * 1024 * 1024);
        ByteFormatter.Format(value, decimals: 0).Should().Be("1 GB");
    }
}
