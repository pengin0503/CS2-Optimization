using CS2Optimization.Core;
using Xunit;

namespace CS2Optimization.Core.Tests;

public sealed class ShadowCascadeClampTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(120f)]
    [InlineData(300f)]
    public void GameAlreadyWithinLimit_NoOverride(float gameDistance)
    {
        Assert.False(ShadowCascadeClamp.TryClamp(gameDistance, 0.2f, 0.46f, 0.6f, 300f, out _));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(-1f)]
    [InlineData(float.PositiveInfinity)]
    public void UnusableGameDistance_NoOverride(float gameDistance)
    {
        Assert.False(ShadowCascadeClamp.TryClamp(gameDistance, 0.2f, 0.46f, 0.6f, 300f, out _));
    }

    [Fact]
    public void InvalidLimit_NoOverride()
    {
        Assert.False(ShadowCascadeClamp.TryClamp(5000f, 0.2f, 0.46f, 0.6f, 0f, out _));
    }

    [Fact]
    public void NearSplitsKeepTheirAbsoluteDistance()
    {
        // Game table F, lowest row: 1300 m, splits 0.01/0.03/0.2 -> 13 m, 39 m, 260 m.
        Assert.True(ShadowCascadeClamp.TryClamp(1300f, 0.01f, 0.03f, 0.2f, 300f, out var c));

        Assert.Equal(300f, c.Distance);
        Assert.Equal(13f, c.Split0 * c.Distance, 3);
        Assert.Equal(39f, c.Split1 * c.Distance, 3);
        Assert.Equal(260f, c.Split2 * c.Distance, 3);
    }

    [Fact]
    public void SplitsBeyondTheLimit_AreSpreadOverWhatRemains()
    {
        // 14000 m with 0.36/0.64/0.76: every boundary lies beyond 300 m.
        Assert.True(ShadowCascadeClamp.TryClamp(14000f, 0.36f, 0.64f, 0.76f, 300f, out var c));

        Assert.Equal(0.25f, c.Split0, 4);
        Assert.Equal(0.5f, c.Split1, 4);
        Assert.Equal(0.75f, c.Split2, 4);
    }

    [Fact]
    public void MixedSplits_KeepNearOnesAndSpreadTheRest()
    {
        // 480 m, 0.36/0.57/0.7 -> 172.8 m, 273.6 m, 336 m (last one beyond 300 m).
        Assert.True(ShadowCascadeClamp.TryClamp(480f, 0.36f, 0.57f, 0.7f, 300f, out var c));

        Assert.Equal(0.576f, c.Split0, 4);
        Assert.Equal(0.912f, c.Split1, 4);
        Assert.Equal(0.956f, c.Split2, 4);
    }

    [Theory]
    [InlineData(0f, 0f, 0f)]
    [InlineData(0.5f, 0.2f, 0.9f)]
    [InlineData(1.5f, 2f, 3f)]
    [InlineData(float.NaN, 0.1f, 0.2f)]
    public void ResultIsAlwaysStrictlyIncreasingInsideZeroToOne(float s0, float s1, float s2)
    {
        Assert.True(ShadowCascadeClamp.TryClamp(5000f, s0, s1, s2, 300f, out var c));

        Assert.True(c.Split0 > 0f && c.Split0 < c.Split1 && c.Split1 < c.Split2 && c.Split2 < 1f,
            $"{c.Split0} {c.Split1} {c.Split2}");
    }
}
