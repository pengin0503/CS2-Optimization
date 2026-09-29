using CS2Optimization.Core;
using Xunit;

namespace CS2Optimization.Core.Tests;

public sealed class EffectiveProfilePolicyTests
{
    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 0)]
    public void Auto_UsesShadowDistanceAndProtectsWriteEverywhereProps(bool hasWe, int expectedOffset)
    {
        var profile = Resolve(Mode.Auto, hasWe: hasWe);

        Assert.Equal(300, profile.ShadowMetres);
        Assert.Equal(expectedOffset, profile.PropLodOffset);
        Assert.Equal(StopReason.None, profile.StopReason);
    }

    [Fact]
    public void Off_IgnoresSavedCustomValuesAndDisablesBothChanges()
    {
        var profile = Resolve(Mode.Off, customShadow: 100, customOffset: 12);

        Assert.Equal(0, profile.ShadowMetres);
        Assert.Equal(0, profile.PropLodOffset);
        Assert.Equal(StopReason.None, profile.StopReason);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 6)]
    [InlineData(175, 12)]
    [InlineData(300, 0)]
    [InlineData(500, 6)]
    public void Custom_UsesListedValues(int shadow, int offset)
    {
        var profile = Resolve(Mode.Custom, customShadow: shadow, customOffset: offset, hasWe: true);

        Assert.Equal(shadow, profile.ShadowMetres);
        Assert.Equal(offset, profile.PropLodOffset);
        Assert.Equal(StopReason.None, profile.StopReason);
    }

    [Fact]
    public void ShadowsDisabled_LeavesPropsEnabledButDoesNotTurnShadowsOn()
    {
        var profile = Resolve(Mode.Auto, shadowsEnabled: false);

        Assert.Equal(0, profile.ShadowMetres);
        Assert.Equal(6, profile.PropLodOffset);
    }

    [Fact]
    public void Cs2SaverConflict_StopsAllChanges()
    {
        var profile = Resolve(Mode.Auto, hasSaver: true);

        Assert.Equal(0, profile.ShadowMetres);
        Assert.Equal(0, profile.PropLodOffset);
        Assert.Equal(StopReason.Cs2SaverConflict, profile.StopReason);
    }

    [Fact]
    public void UnsupportedGame_StopsAllChanges()
    {
        var profile = Resolve(Mode.Auto, supportedGame: false);

        Assert.Equal(0, profile.ShadowMetres);
        Assert.Equal(0, profile.PropLodOffset);
        Assert.Equal(StopReason.UnsupportedGame, profile.StopReason);
    }

    [Theory]
    [InlineData(200, 6)]
    [InlineData(300, 7)]
    [InlineData(-1, 0)]
    public void InvalidCustomValues_FailClosed(int shadow, int offset)
    {
        var profile = Resolve(Mode.Custom, customShadow: shadow, customOffset: offset);

        Assert.Equal(0, profile.ShadowMetres);
        Assert.Equal(0, profile.PropLodOffset);
        Assert.Equal(StopReason.InvalidSettings, profile.StopReason);
    }

    [Fact]
    public void UnknownMode_FailsClosed()
    {
        var profile = Resolve((Mode)999);

        Assert.Equal(0, profile.ShadowMetres);
        Assert.Equal(0, profile.PropLodOffset);
        Assert.Equal(StopReason.InvalidSettings, profile.StopReason);
    }

    [Fact]
    public void SwitchingToCustom_PreservesTheEffectiveAutoValues()
    {
        var current = Resolve(Mode.Auto, hasWe: true);

        var values = EffectiveProfilePolicy.ToCustom(current);

        Assert.Equal(300, values.shadowMetres);
        Assert.Equal(0, values.propLodOffset);
    }

    private static EffectiveProfile Resolve(
        Mode mode,
        int customShadow = 0,
        int customOffset = 0,
        bool hasWe = false,
        bool hasSaver = false,
        bool supportedGame = true,
        bool shadowsEnabled = true) =>
        EffectiveProfilePolicy.Resolve(
            mode, customShadow, customOffset, hasWe, hasSaver, supportedGame, shadowsEnabled);
}
