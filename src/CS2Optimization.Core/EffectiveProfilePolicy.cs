namespace CS2Optimization.Core
{
    /// <summary>The player's saved choice. Only these three values are valid.</summary>
    public enum Mode
    {
        /// <summary>New-install default: shadows 300 m, small props +6 unless Write Everywhere is loaded.</summary>
        Auto = 0,

        /// <summary>Leave every rendering value to the game.</summary>
        Off = 1,

        /// <summary>The player picks the shadow distance and prop offset from the listed values.</summary>
        Custom = 2,
    }

    /// <summary>Why nothing is being changed even though the mode asks for a change.</summary>
    public enum StopReason
    {
        None = 0,

        /// <summary>Cs2Saver is loaded; both mods would fight over the same values.</summary>
        Cs2SaverConflict = 1,

        /// <summary>The running game build has not been verified against this mod.</summary>
        UnsupportedGame = 2,

        /// <summary>The saved mode or custom values are not one of the listed choices.</summary>
        InvalidSettings = 3,
    }

    /// <summary>
    /// What the mod will actually do, plus what the mode asked for before anything stopped it.
    /// A value of 0 always means "leave the game's own value alone".
    /// </summary>
    public readonly struct EffectiveProfile
    {
        public EffectiveProfile(
            int shadowMetres,
            int propLodOffset,
            StopReason stopReason,
            int requestedShadowMetres,
            int requestedPropLodOffset)
        {
            ShadowMetres = shadowMetres;
            PropLodOffset = propLodOffset;
            StopReason = stopReason;
            RequestedShadowMetres = requestedShadowMetres;
            RequestedPropLodOffset = requestedPropLodOffset;
        }

        /// <summary>Shadow distance applied right now, after every gate. 0 = unchanged.</summary>
        public int ShadowMetres { get; }

        /// <summary>LOD floor offset applied to eligible small props right now. 0 = unchanged.</summary>
        public int PropLodOffset { get; }

        public StopReason StopReason { get; }

        /// <summary>Shadow distance the mode asks for, before stop reasons and the game's shadow switch.</summary>
        public int RequestedShadowMetres { get; }

        /// <summary>Prop offset the mode asks for, before stop reasons.</summary>
        public int RequestedPropLodOffset { get; }

        /// <summary>
        /// The shadow limit to hand to the shadow controller. It re-checks the game's shadow
        /// switch on its own, so this is gated by stop reasons only.
        /// </summary>
        public int ShadowRequestWhenShadowsOn =>
            StopReason == StopReason.None ? RequestedShadowMetres : 0;
    }

    /// <summary>
    /// Turns the saved settings and what was detected at runtime into the values the two
    /// rendering controllers receive. Pure: no game types, no side effects.
    /// </summary>
    public static class EffectiveProfilePolicy
    {
        public const int AutoShadowMetres = 300;
        public const int AutoPropLodOffset = 6;

        /// <summary>Shadow distances offered in Custom. 0 = unchanged.</summary>
        public static readonly int[] ShadowChoices = { 0, 100, 175, 300, 500 };

        /// <summary>Prop LOD offsets offered in Custom. Each +6 halves the draw distance.</summary>
        public static readonly int[] PropOffsetChoices = { 0, 6, 12 };

        public static bool IsValidShadow(int metres) => System.Array.IndexOf(ShadowChoices, metres) >= 0;

        public static bool IsValidPropOffset(int offset) => System.Array.IndexOf(PropOffsetChoices, offset) >= 0;

        public static EffectiveProfile Resolve(
            Mode mode,
            int customShadowMetres,
            int customPropLodOffset,
            bool writeEverywhereLoaded,
            bool cs2SaverLoaded,
            bool supportedGameVersion,
            bool gameShadowsEnabled)
        {
            int shadow;
            int offset;

            switch (mode)
            {
                case Mode.Auto:
                    shadow = AutoShadowMetres;
                    // Write Everywhere draws text on props; cutting their distance would make
                    // signs vanish early, so Auto leaves them alone when it is present.
                    offset = writeEverywhereLoaded ? 0 : AutoPropLodOffset;
                    break;
                case Mode.Off:
                    shadow = 0;
                    offset = 0;
                    break;
                case Mode.Custom:
                    if (!IsValidShadow(customShadowMetres) || !IsValidPropOffset(customPropLodOffset))
                    {
                        return Stopped(StopReason.InvalidSettings, 0, 0);
                    }

                    shadow = customShadowMetres;
                    offset = customPropLodOffset;
                    break;
                default:
                    return Stopped(StopReason.InvalidSettings, 0, 0);
            }

            // Conflict outranks version: with Cs2Saver present the answer is "remove one of
            // them" whatever the game build is.
            if (cs2SaverLoaded) return Stopped(StopReason.Cs2SaverConflict, shadow, offset);
            if (!supportedGameVersion) return Stopped(StopReason.UnsupportedGame, shadow, offset);

            // Bound the shadow only when the game already draws one; never switch shadows on.
            var appliedShadow = gameShadowsEnabled ? shadow : 0;
            return new EffectiveProfile(appliedShadow, offset, StopReason.None, shadow, offset);
        }

        /// <summary>
        /// Values to seed Custom with when the player switches to it, so the picture does not
        /// jump. Uses what the mode asked for rather than what a temporary gate (game shadows
        /// off, a conflict) reduced it to, so the copy stays meaningful once the gate lifts.
        /// </summary>
        public static (int shadowMetres, int propLodOffset) ToCustom(EffectiveProfile current) =>
            (current.RequestedShadowMetres, current.RequestedPropLodOffset);

        private static EffectiveProfile Stopped(StopReason reason, int requestedShadow, int requestedOffset) =>
            new EffectiveProfile(0, 0, reason, requestedShadow, requestedOffset);
    }
}
