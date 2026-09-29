using System.Linq;
using CS2Optimization.Core;
using Xunit;

namespace CS2Optimization.Core.Tests;

public sealed class LodLedgerTests
{
    [Theory]
    [InlineData(120, 6, 126)]
    [InlineData(120, 12, 132)]
    [InlineData(120, 0, 120)]
    [InlineData(254, 12, 255)]
    [InlineData(255, 6, 255)]
    [InlineData(0, 0, 0)]
    public void Target_AddsOffsetAndClampsTo255(int original, int offset, int expected)
    {
        Assert.Equal(expected, LodLedger<int>.Target(original, offset));
    }

    [Fact]
    public void RepeatedApplication_DoesNotStackOffsets()
    {
        var ledger = new LodLedger<int>();
        var current = 120;

        for (var i = 0; i < 5; i++)
        {
            if (ledger.Plan(1, current, 6, out var write)) current = write;
        }

        Assert.Equal(126, current);
        Assert.Equal(120, ledger.GetOrCapture(1, current));
    }

    [Fact]
    public void ChangingOffset_IsMeasuredFromTheOriginal()
    {
        var ledger = new LodLedger<int>();
        var current = 120;

        if (ledger.Plan(1, current, 6, out var w1)) current = w1;
        if (ledger.Plan(1, current, 12, out var w2)) current = w2;
        Assert.Equal(132, current);

        if (ledger.Plan(1, current, 0, out var w3)) current = w3;
        Assert.Equal(120, current);
    }

    [Fact]
    public void Plan_ReportsNoWriteWhenValueAlreadyMatches()
    {
        var ledger = new LodLedger<int>();

        Assert.True(ledger.Plan(1, 120, 6, out _));
        Assert.False(ledger.Plan(1, 126, 6, out _));
        Assert.False(ledger.Plan(2, 90, 0, out _));
    }

    [Fact]
    public void GameRewritingTheValue_BecomesTheNewOriginal()
    {
        var ledger = new LodLedger<int>();
        ledger.Plan(1, 120, 6, out _);

        // The game re-derived the prefab's floor behind our back (e.g. prefab re-initialised).
        Assert.True(ledger.Plan(1, 100, 6, out var write));

        Assert.Equal(106, write);
        Assert.Equal(100, ledger.Entries.Single().Value);
    }

    [Fact]
    public void Restore_ReturnsOriginalOnlyWhileOurValueIsStillInPlace()
    {
        var ledger = new LodLedger<int>();
        ledger.Plan(1, 120, 6, out _);
        ledger.Plan(2, 110, 6, out _);

        Assert.True(ledger.TryRestore(1, 126, out var original));
        Assert.Equal(120, original);

        // Someone else changed it since; writing 110 back would clobber their value.
        Assert.False(ledger.TryRestore(2, 99, out _));
    }

    [Fact]
    public void Restore_ThenReapply_StillUsesTheOriginal()
    {
        var ledger = new LodLedger<int>();
        ledger.Plan(1, 120, 6, out _);
        ledger.TryRestore(1, 126, out var restored);

        Assert.True(ledger.Plan(1, restored, 12, out var write));
        Assert.Equal(132, write);
    }

    [Fact]
    public void Restore_OfUnknownId_DoesNothing()
    {
        var ledger = new LodLedger<int>();

        Assert.False(ledger.TryRestore(42, 126, out _));
    }

    [Fact]
    public void Forget_DropsTheEntry()
    {
        var ledger = new LodLedger<int>();
        ledger.Plan(1, 120, 6, out _);

        ledger.Forget(1);

        Assert.Equal(0, ledger.Count);
        Assert.False(ledger.TryRestore(1, 126, out _));
    }

    [Fact]
    public void Clear_LetsAReusedIdCaptureAFreshOriginal()
    {
        var ledger = new LodLedger<int>();
        ledger.Plan(7, 120, 6, out _);

        ledger.Clear();

        // Same id, different city: its value must not be mistaken for the old prefab's.
        Assert.True(ledger.Plan(7, 126, 6, out var write));
        Assert.Equal(132, write);
        Assert.Equal(126, ledger.GetOrCapture(7, 132));
    }

    [Fact]
    public void Entries_ListIdsWithTheirOriginals()
    {
        var ledger = new LodLedger<int>();
        ledger.Plan(1, 120, 6, out _);
        ledger.Plan(2, 90, 12, out _);

        var entries = ledger.Entries.OrderBy(e => e.Key).ToArray();

        Assert.Equal(new[] { 1, 2 }, entries.Select(e => e.Key));
        Assert.Equal(new[] { 120, 90 }, entries.Select(e => e.Value));
    }
}
