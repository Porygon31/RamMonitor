using FluentAssertions;
using RamMonitor.Core.Utils;
using Xunit;

namespace RamMonitor.Tests;

/// <summary>
/// Tests de <see cref="LinearRegression.Fit"/>.
/// </summary>
public sealed class LinearRegressionTests
{
    [Fact]
    public void Fit_PerfectLine_ReturnsExactSlopeInterceptAndR2Equal1()
    {
        var xs = new double[] { 0, 1, 2, 3, 4 };
        var ys = new double[] { 3, 5, 7, 9, 11 };

        var result = LinearRegression.Fit(xs, ys);

        result.Slope.Should().BeApproximately(2.0, 1e-9);
        result.Intercept.Should().BeApproximately(3.0, 1e-9);
        result.RSquared.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Fit_NegativeSlope_ReturnsNegativeSlopeAndR2Equal1()
    {
        var xs = new double[] { 0, 1, 2, 3, 4 };
        var ys = new double[] { 10, 7, 4, 1, -2 };

        var result = LinearRegression.Fit(xs, ys);

        result.Slope.Should().BeApproximately(-3.0, 1e-9);
        result.Intercept.Should().BeApproximately(10.0, 1e-9);
        result.RSquared.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Fit_FlatLine_ReturnsZeroSlopeAndR2Defined()
    {
        var xs = new double[] { 0, 1, 2, 3, 4 };
        var ys = new double[] { 5, 5, 5, 5, 5 };

        var result = LinearRegression.Fit(xs, ys);

        result.Slope.Should().BeApproximately(0.0, 1e-9);
        result.Intercept.Should().BeApproximately(5.0, 1e-9);
        result.RSquared.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Fit_NoisyLine_R2BetweenZeroAndOne()
    {
        var xs = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var ys = new double[] { 0.1, 0.9, 2.2, 2.8, 4.1, 5.2, 5.8, 7.0, 8.3, 8.9 };

        var result = LinearRegression.Fit(xs, ys);

        result.Slope.Should().BeGreaterThan(0.9).And.BeLessThan(1.1);
        result.RSquared.Should().BeGreaterThan(0.9).And.BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void Fit_SinglePoint_ReturnsZeroSlopeAndInterceptEqualToY()
    {
        var xs = new double[] { 7 };
        var ys = new double[] { 42 };

        var result = LinearRegression.Fit(xs, ys);

        result.Slope.Should().Be(0.0);
        result.Intercept.Should().Be(42.0);
        result.RSquared.Should().Be(0.0);
    }

    [Fact]
    public void Fit_MismatchedLengths_Throws()
    {
        var xs = new double[] { 1, 2, 3 };
        var ys = new double[] { 1, 2 };

        Action act = () =>
        {
            var _ = LinearRegression.Fit(xs, ys);
        };

        act.Should().Throw<ArgumentException>();
    }
}
