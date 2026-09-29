namespace CS2Optimization.Core
{
    /// <summary>Decides the value for this mod's own shadow-distance override, if any.</summary>
    public static class ShadowOverrideState
    {
        /// <inheritdoc cref="Resolve(int, bool?, float?)"/>
        public static float? Resolve(int requestedMetres, bool gameShadowsEnabled) =>
            Resolve(requestedMetres, gameShadowsEnabled, null);

        /// <summary>
        /// Returns the distance to override with, or null for "no override". Never overrides
        /// when the game's shadows are off or their state is unknown (this bounds shadows, it
        /// never turns them on), and never raises a distance the game already keeps shorter.
        /// </summary>
        /// <param name="baselineMetres">
        /// The distance in effect without this mod's override, when it could be read. Null or
        /// a non-positive / NaN value means unknown.
        /// </param>
        public static float? Resolve(int requestedMetres, bool? gameShadowsEnabled, float? baselineMetres)
        {
            if (requestedMetres == 0 || !EffectiveProfilePolicy.IsValidShadow(requestedMetres)) return null;
            if (gameShadowsEnabled != true) return null;

            var baseline = baselineMetres ?? float.NaN;
            if (baseline > 0f && baseline <= requestedMetres) return null;

            return requestedMetres;
        }
    }
}
