namespace CS2Optimization.Core
{
    /// <summary>A shadow distance with its three cascade split ratios (fractions of the distance).</summary>
    public readonly struct ShadowCascades
    {
        public ShadowCascades(float distance, float split0, float split1, float split2)
        {
            Distance = distance;
            Split0 = split0;
            Split1 = split1;
            Split2 = split2;
        }

        public float Distance { get; }

        public float Split0 { get; }

        public float Split1 { get; }

        public float Split2 { get; }
    }

    /// <summary>
    /// Limits the game's shadow distance without disturbing its cascades.
    ///
    /// <para>The game recomputes the distance and split ratios every frame from camera height
    /// and pitch (<c>CameraUpdateSystem.UpdateShadowsCascades</c>: from 0 m looking down up to
    /// 14 000 m looking at the horizon). Replacing the distance alone would keep the ratios, so
    /// a 1% split meant for 1300 m would shrink to 3 m. Instead every split boundary keeps its
    /// absolute distance while it lies inside the limit; boundaries beyond the limit are spread
    /// evenly over the part that is left.</para>
    /// </summary>
    public static class ShadowCascadeClamp
    {
        /// <summary>
        /// False when nothing needs overriding: the game's distance is already within the limit
        /// (so it is never extended) or either value is unusable.
        /// </summary>
        public static bool TryClamp(float gameDistance, float split0, float split1, float split2, float limit,
            out ShadowCascades result)
        {
            result = default;
            if (!(limit > 0f) || float.IsInfinity(limit)) return false;
            if (!(gameDistance > limit) || float.IsInfinity(gameDistance)) return false;

            var scale = gameDistance / limit;
            var s0 = Place(split0 * scale, 0f, 3);
            var s1 = Place(split1 * scale, s0, 2);
            var s2 = Place(split2 * scale, s1, 1);

            result = new ShadowCascades(limit, s0, s1, s2);
            return true;
        }

        /// <summary>Keeps <paramref name="ratio"/> if it still fits, otherwise spreads the rest evenly.</summary>
        private static float Place(float ratio, float previous, int remaining)
        {
            if (ratio > previous && ratio < 1f) return ratio;
            return previous + (1f - previous) / (remaining + 1);
        }
    }
}
