namespace CS2Optimization.Core
{
    /// <summary>What happens to the saved Custom values when settings are applied.</summary>
    public static class SettingsTransition
    {
        public const Mode DefaultMode = Mode.Auto;

        public readonly struct Result
        {
            public Result(int customShadowMetres, int customPropLodOffset, bool copiedFromPrevious)
            {
                CustomShadowMetres = customShadowMetres;
                CustomPropLodOffset = customPropLodOffset;
                CopiedFromPrevious = copiedFromPrevious;
            }

            public int CustomShadowMetres { get; }

            public int CustomPropLodOffset { get; }

            /// <summary>True when the Custom values were just seeded and need saving.</summary>
            public bool CopiedFromPrevious { get; }
        }

        /// <summary>
        /// Switching into Custom from another mode seeds Custom with what that mode asked for, so
        /// the picture does not jump. Every other case keeps the saved values as they are; in
        /// particular the first apply after startup (<paramref name="previousMode"/> null) never
        /// rewrites what the player saved.
        /// </summary>
        public static Result OnApply(
            Mode? previousMode,
            Mode nextMode,
            EffectiveProfile previousProfile,
            int customShadowMetres,
            int customPropLodOffset)
        {
            if (previousMode.HasValue && previousMode.Value != Mode.Custom && nextMode == Mode.Custom)
            {
                var (shadow, offset) = EffectiveProfilePolicy.ToCustom(previousProfile);
                return new Result(shadow, offset, true);
            }

            return new Result(customShadowMetres, customPropLodOffset, false);
        }
    }
}
