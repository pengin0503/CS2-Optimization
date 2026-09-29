namespace CS2Optimization.Core
{
    /// <summary>Game builds this mod has been checked against. Anything else: change nothing.</summary>
    public static class GameVersionPolicy
    {
        public static readonly string[] Supported = { "1.6.0f1" };

        public static bool IsSupported(string? version)
        {
            if (version == null) return false;
            var trimmed = version.Trim();
            foreach (var supported in Supported)
            {
                if (string.Equals(trimmed, supported, System.StringComparison.Ordinal)) return true;
            }

            return false;
        }
    }
}
