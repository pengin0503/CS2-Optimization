using CS2Optimization.Core;
using Xunit;

namespace CS2Optimization.Core.Tests;

public sealed class StatusTextTests
{
    [Theory]
    [InlineData(StopReason.Cs2SaverConflict, "Cs2Saver")]
    [InlineData(StopReason.UnsupportedGame, "9.9.9f9")]
    [InlineData(StopReason.InvalidSettings, "Custom")]
    public void StoppedStatus_NamesTheReason(StopReason reason, string expectedFragment)
    {
        var state = Sample() with { StopReason = reason, GameVersion = "9.9.9f9" };

        Assert.Contains(expectedFragment, StatusText.Describe(state, japanese: false));
        Assert.Contains(expectedFragment, StatusText.Describe(state, japanese: true));
    }

    [Fact]
    public void Running_ShowsRequestedAndApplied()
    {
        var text = StatusText.Describe(Sample(), japanese: false);

        Assert.Contains("300 m", text);
        Assert.Contains("+6", text);
    }

    [Fact]
    public void GameShadowsOff_SaysTheShadowFeatureIsInactive()
    {
        var state = Sample() with { GameShadowsEnabled = false, AppliedShadowMetres = null };

        Assert.Contains("影機能は無効", StatusText.Describe(state, japanese: true));
        Assert.Contains("shadows are off", StatusText.Describe(state, japanese: false));
    }

    [Fact]
    public void WriteEverywhere_IsMentionedWhenItProtectsProps()
    {
        var state = Sample() with { WriteEverywhere = true, RequestedPropLodOffset = 0, AppliedPropLodOffset = 0 };

        Assert.Contains("Write Everywhere", StatusText.Describe(state, japanese: false));
        Assert.Contains("Write Everywhere", StatusText.Describe(state, japanese: true));
    }

    [Fact]
    public void PropChangeWaitingForReload_IsExplained()
    {
        var state = Sample() with { PropReloadPending = true };

        Assert.Contains("再ロード", StatusText.Describe(state, japanese: true));
        Assert.Contains("reload", StatusText.Describe(state, japanese: false));
    }

    [Fact]
    public void GameShadowAlreadyShorter_SaysSo()
    {
        var state = Sample() with { AppliedShadowMetres = null, GameShadowMetres = 120f };

        Assert.Contains("120 m", StatusText.Describe(state, japanese: false));
        Assert.Contains("120 m", StatusText.Describe(state, japanese: true));
    }

    [Fact]
    public void FeatureFailure_IsShown()
    {
        var state = Sample() with { PropFeatureFailed = true };

        Assert.Contains("stopped", StatusText.Describe(state, japanese: false));
        Assert.Contains("停止", StatusText.Describe(state, japanese: true));
    }

    private static StatusState Sample() => new StatusState
    {
        StopReason = StopReason.None,
        GameVersion = "1.6.2f1",
        WriteEverywhere = false,
        GameShadowsEnabled = true,
        RequestedShadowMetres = 300,
        AppliedShadowMetres = 300f,
        RequestedPropLodOffset = 6,
        AppliedPropLodOffset = 6,
    };
}
