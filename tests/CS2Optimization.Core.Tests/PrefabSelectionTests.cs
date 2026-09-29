using System.Collections.Generic;
using CS2Optimization.Core;
using Xunit;

namespace CS2Optimization.Core.Tests;

public sealed class PrefabSelectionTests
{
    [Theory]
    //          static  net    building tree   plant  eligible
    [InlineData(true,  false, false,   false, false, true)]  // bench, sign
    [InlineData(false, true,  false,   false, false, true)]  // street lamp on a road
    [InlineData(true,  true,  false,   false, false, true)]
    [InlineData(true,  false, true,    false, false, false)] // building
    [InlineData(false, true,  true,    false, false, false)]
    [InlineData(true,  false, false,   true,  false, false)] // tree
    [InlineData(true,  false, false,   false, true,  false)] // plant
    [InlineData(true,  false, false,   true,  true,  false)]
    [InlineData(false, false, false,   false, false, false)] // vehicle/creature: neither static nor net
    public void OnlySmallStaticOrNetPropsAreEligible(
        bool hasStatic, bool hasNet, bool hasBuilding, bool hasTree, bool hasPlant, bool expected)
    {
        Assert.Equal(expected, PrefabCategoryPolicy.IsEligible(hasStatic, hasNet, hasBuilding, hasTree, hasPlant));
    }

    [Fact]
    public void TurningOff_RestoresEveryOriginal()
    {
        var store = new Dictionary<int, int> { [1] = 120, [2] = 90 };
        var ledger = new LodLedger<int>();
        Apply(ledger, store, 6);
        Assert.Equal(126, store[1]);

        var result = ledger.RestoreAll(Read(store), (id, v) => store[id] = v);

        Assert.Equal(2, result.Restored);
        Assert.Equal(120, store[1]);
        Assert.Equal(90, store[2]);
    }

    [Fact]
    public void Restore_SkipsEntitiesThatNoLongerExist()
    {
        var store = new Dictionary<int, int> { [1] = 120, [2] = 90 };
        var ledger = new LodLedger<int>();
        Apply(ledger, store, 6);
        store.Remove(2);
        var written = new List<int>();

        var result = ledger.RestoreAll(Read(store), (id, v) => { written.Add(id); store[id] = v; });

        Assert.Equal(new[] { 1 }, written);
        Assert.Equal(1, result.Missing);
        Assert.False(store.ContainsKey(2));
        Assert.Equal(1, ledger.Count);
    }

    [Fact]
    public void Restore_LeavesValuesSomeoneElseChanged()
    {
        var store = new Dictionary<int, int> { [1] = 120 };
        var ledger = new LodLedger<int>();
        Apply(ledger, store, 6);
        store[1] = 140;

        var result = ledger.RestoreAll(Read(store), (id, v) => store[id] = v);

        Assert.Equal(0, result.Restored);
        Assert.Equal(1, result.ChangedElsewhere);
        Assert.Equal(140, store[1]);
    }

    [Fact]
    public void Restore_WhenNothingWasChanged_WritesNothing()
    {
        var store = new Dictionary<int, int> { [1] = 120 };
        var ledger = new LodLedger<int>();
        Apply(ledger, store, 0);

        var result = ledger.RestoreAll(Read(store), (id, v) => store[id] = v);

        Assert.Equal(0, result.Restored);
        Assert.Equal(0, result.ChangedElsewhere);
        Assert.Equal(0, result.Missing);
    }

    [Fact]
    public void CitySwitch_RestoresThenStartsFromTheNewCityValues()
    {
        var cityA = new Dictionary<int, int> { [5] = 120 };
        var ledger = new LodLedger<int>();
        Apply(ledger, cityA, 6);

        ledger.RestoreAll(Read(cityA), (id, v) => cityA[id] = v);
        ledger.Clear();

        // City B reuses id 5 for an unrelated prefab.
        var cityB = new Dictionary<int, int> { [5] = 100 };
        Apply(ledger, cityB, 6);

        Assert.Equal(120, cityA[5]);
        Assert.Equal(106, cityB[5]);
    }

    private static System.Func<int, int?> Read(Dictionary<int, int> store) =>
        id => store.TryGetValue(id, out var v) ? v : null;

    private static void Apply(LodLedger<int> ledger, Dictionary<int, int> store, int offset)
    {
        foreach (var id in new List<int>(store.Keys))
        {
            if (ledger.Plan(id, store[id], offset, out var write)) store[id] = write;
        }
    }
}
