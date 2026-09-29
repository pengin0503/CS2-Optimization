namespace CS2Optimization.Core
{
    /// <summary>
    /// Which prefabs count as "small props" whose draw distance may be shortened. Street
    /// furniture and static clutter qualify; buildings, trees and plants never do, whatever else
    /// they carry. Vehicles and citizens are not static or net objects, so they never match.
    /// </summary>
    public static class PrefabCategoryPolicy
    {
        public static bool IsEligible(bool hasStatic, bool hasNet, bool hasBuilding, bool hasTree, bool hasPlant) =>
            (hasStatic || hasNet) && !hasBuilding && !hasTree && !hasPlant;
    }
}
