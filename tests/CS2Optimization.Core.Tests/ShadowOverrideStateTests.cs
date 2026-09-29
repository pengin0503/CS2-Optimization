using CS2Optimization.Core;
using Xunit;

namespace CS2Optimization.Core.Tests;

public sealed class ShadowOverrideStateTests
{
    [Fact]
    public void ShadowsOff_NoOverride()
    {
        Assert.Null(ShadowOverrideState.Resolve(300, gameShadowsEnabled: false));
    }

    [Fact]
    public void ShadowsUnknown_NoOverride()
    {
        Assert.Null(ShadowOverrideState.Resolve(300, gameShadowsEnabled: null, baselineMetres: 2000f));
    }

    [Fact]
    public void RequestZero_NoOverride()
    {
        Assert.Null(ShadowOverrideState.Resolve(0, gameShadowsEnabled: true));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(175)]
    [InlineData(300)]
    [InlineData(500)]
    public void ShadowsOn_LimitsToTheRequest(int metres)
    {
        Assert.Equal(metres, ShadowOverrideState.Resolve(metres, gameShadowsEnabled: true));
    }

    [Fact]
    public void BackToOff_RemovesTheOverride()
    {
        Assert.Equal(300f, ShadowOverrideState.Resolve(300, true));
        Assert.Null(ShadowOverrideState.Resolve(0, true));
    }

    [Fact]
    public void GameAlreadyShorter_IsNeverRaised()
    {
        Assert.Null(ShadowOverrideState.Resolve(300, true, baselineMetres: 250f));
        Assert.Null(ShadowOverrideState.Resolve(300, true, baselineMetres: 300f));
    }

    [Fact]
    public void GameLonger_IsLimited()
    {
        Assert.Equal(300f, ShadowOverrideState.Resolve(300, true, baselineMetres: 1500f));
    }

    [Fact]
    public void UnknownBaseline_StillLimits()
    {
        Assert.Equal(300f, ShadowOverrideState.Resolve(300, true, baselineMetres: null));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void UnusableBaseline_IsTreatedAsUnknown(float baseline)
    {
        Assert.Equal(300f, ShadowOverrideState.Resolve(300, true, baselineMetres: baseline));
    }

    [Fact]
    public void InvalidRequest_NoOverride()
    {
        Assert.Null(ShadowOverrideState.Resolve(-1, true));
        Assert.Null(ShadowOverrideState.Resolve(200, true));
    }
}
