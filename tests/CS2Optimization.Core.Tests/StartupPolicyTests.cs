using CS2Optimization.Core;
using Xunit;

namespace CS2Optimization.Core.Tests;

public sealed class StartupPolicyTests
{
    [Theory]
    [InlineData("1.6.2f1", true)]
    [InlineData(" 1.6.2f1 ", true)]
    [InlineData("1.6.0f1", false)]
    [InlineData("1.6.2f2", false)]
    [InlineData("1.6.2", false)]
    [InlineData("1.6.2f10", false)]
    [InlineData("1.6.2f1 (767.21d1)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyVerifiedGameVersionsAreSupported(string? version, bool expected)
    {
        Assert.Equal(expected, GameVersionPolicy.IsSupported(version));
    }

    [Fact]
    public void DetectsWriteEverywhereAndCs2SaverByAssemblyName()
    {
        var presence = ModPresence.Detect(new[] { "Game", "BelzontWE", "Cs2Saver", "CS2Optimization" });

        Assert.True(presence.WriteEverywhere);
        Assert.True(presence.Cs2Saver);
    }

    [Fact]
    public void SimilarNamesAreNotMistakenForTheMods()
    {
        var presence = ModPresence.Detect(new[] { "BelzontWE.Extras", "Cs2SaverTools", null!, "" });

        Assert.False(presence.WriteEverywhere);
        Assert.False(presence.Cs2Saver);
    }

    [Fact]
    public void FirstInstall_DefaultsToAuto()
    {
        Assert.Equal(Mode.Auto, SettingsTransition.DefaultMode);
    }

    [Fact]
    public void Startup_DoesNotRewriteSavedCustomValues()
    {
        var result = SettingsTransition.OnApply(
            previousMode: null, nextMode: Mode.Custom,
            previousProfile: default, customShadowMetres: 175, customPropLodOffset: 12);

        Assert.False(result.CopiedFromPrevious);
        Assert.Equal(175, result.CustomShadowMetres);
        Assert.Equal(12, result.CustomPropLodOffset);
    }

    [Fact]
    public void Startup_WithSavedOff_StaysOff()
    {
        var profile = EffectiveProfilePolicy.Resolve(Mode.Off, 0, 0, false, false, true, true);

        Assert.Equal(0, profile.ShadowMetres);
        Assert.Equal(0, profile.PropLodOffset);
    }

    [Fact]
    public void SwitchingAutoToCustom_CopiesTheEffectiveAutoValues()
    {
        var auto = EffectiveProfilePolicy.Resolve(Mode.Auto, 0, 0, writeEverywhereLoaded: true, false, true, true);

        var result = SettingsTransition.OnApply(Mode.Auto, Mode.Custom, auto, 500, 12);

        Assert.True(result.CopiedFromPrevious);
        Assert.Equal(300, result.CustomShadowMetres);
        Assert.Equal(0, result.CustomPropLodOffset);
    }

    [Fact]
    public void ChangingCustomValues_WhileInCustom_KeepsThem()
    {
        var custom = EffectiveProfilePolicy.Resolve(Mode.Custom, 175, 6, false, false, true, true);

        var result = SettingsTransition.OnApply(Mode.Custom, Mode.Custom, custom, 100, 12);

        Assert.False(result.CopiedFromPrevious);
        Assert.Equal(100, result.CustomShadowMetres);
        Assert.Equal(12, result.CustomPropLodOffset);
    }

    [Fact]
    public void LeavingCustom_KeepsCustomValuesForLater()
    {
        var custom = EffectiveProfilePolicy.Resolve(Mode.Custom, 175, 6, false, false, true, true);

        var result = SettingsTransition.OnApply(Mode.Custom, Mode.Off, custom, 175, 6);

        Assert.False(result.CopiedFromPrevious);
        Assert.Equal(175, result.CustomShadowMetres);
        Assert.Equal(6, result.CustomPropLodOffset);
    }

    [Fact]
    public void WarnOnce_ReportsEachKeyOnlyOnce()
    {
        var warn = new WarnOnce();

        Assert.True(warn.ShouldLog("shadow-probe"));
        Assert.False(warn.ShouldLog("shadow-probe"));
        Assert.True(warn.ShouldLog("prefab-pass"));

        warn.Reset();
        Assert.True(warn.ShouldLog("shadow-probe"));
    }
}
