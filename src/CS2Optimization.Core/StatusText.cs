using System.Text;

namespace CS2Optimization.Core
{
    /// <summary>Everything the options page needs to explain what the mod is doing right now.</summary>
    public struct StatusState
    {
        public StopReason StopReason { get; set; }

        public string? GameVersion { get; set; }

        public bool WriteEverywhere { get; set; }

        /// <summary>The game's own shadow switch; null when it could not be read.</summary>
        public bool? GameShadowsEnabled { get; set; }

        public int RequestedShadowMetres { get; set; }

        /// <summary>The override actually in effect; null when the game's distance is untouched.</summary>
        public float? AppliedShadowMetres { get; set; }

        public int RequestedPropLodOffset { get; set; }

        public int AppliedPropLodOffset { get; set; }

        public bool ShadowFeatureFailed { get; set; }

        public bool PropFeatureFailed { get; set; }
    }

    /// <summary>Builds the status line in Japanese or English. Pure, so it can be tested.</summary>
    public static class StatusText
    {
        public static string Describe(StatusState s, bool japanese)
        {
            var sb = new StringBuilder();

            switch (s.StopReason)
            {
                case StopReason.Cs2SaverConflict:
                    return japanese
                        ? "停止中: Cs2Saver が導入されています。描画の変更は行いません。どちらか一方をオフにしてください。"
                        : "Stopped: Cs2Saver is installed. Nothing is changed; disable one of the two mods.";
                case StopReason.UnsupportedGame:
                    return japanese
                        ? $"停止中: 未検証のゲーム版 {s.GameVersion} です（対応: {string.Join(", ", GameVersionPolicy.Supported)}）。描画の変更は行いません。"
                        : $"Stopped: game version {s.GameVersion} is not verified (supported: {string.Join(", ", GameVersionPolicy.Supported)}). Nothing is changed.";
                case StopReason.InvalidSettings:
                    return japanese
                        ? "停止中: 保存された設定値が不正です（Mode/Custom）。描画の変更は行いません。設定を選び直してください。"
                        : "Stopped: saved settings are invalid (Mode/Custom). Nothing is changed; please choose the settings again.";
            }

            // Shadows
            if (s.ShadowFeatureFailed)
            {
                sb.Append(japanese ? "影: エラーのため停止" : "Shadows: stopped after an error");
            }
            else if (s.GameShadowsEnabled == false)
            {
                sb.Append(japanese ? "影: ゲーム設定で影がオフのため影機能は無効" : "Shadows: the game's shadows are off, so this feature is inactive");
            }
            else if (s.GameShadowsEnabled == null && s.RequestedShadowMetres != 0)
            {
                sb.Append(japanese ? "影: ゲームの影設定を確認できないため無効" : "Shadows: inactive because the game's shadow setting could not be read");
            }
            else
            {
                sb.Append(japanese ? "影: 要求 " : "Shadows: requested ");
                sb.Append(Metres(s.RequestedShadowMetres, japanese));
                sb.Append(japanese ? " / 適用 " : " / applied ");
                sb.Append(s.AppliedShadowMetres.HasValue
                    ? $"{s.AppliedShadowMetres.Value:0} m"
                    : (japanese ? "無変更" : "unchanged"));
            }

            sb.Append(japanese ? "　｜　" : "  |  ");

            // Props
            if (s.PropFeatureFailed)
            {
                sb.Append(japanese ? "小物: エラーのため停止" : "Props: stopped after an error");
            }
            else
            {
                sb.Append(japanese ? "小物LOD: 要求 " : "Prop LOD: requested ");
                sb.Append(Offset(s.RequestedPropLodOffset, japanese));
                sb.Append(japanese ? " / 適用 " : " / applied ");
                sb.Append(Offset(s.AppliedPropLodOffset, japanese));
            }

            if (s.WriteEverywhere)
            {
                sb.Append(japanese
                    ? "　｜　Write Everywhere を検出（Auto では小物を変更しません）"
                    : "  |  Write Everywhere detected (Auto leaves props alone)");
            }

            return sb.ToString();
        }

        private static string Metres(int metres, bool japanese) =>
            metres == 0 ? (japanese ? "無変更" : "unchanged") : $"{metres} m";

        private static string Offset(int offset, bool japanese) =>
            offset == 0 ? (japanese ? "無変更" : "unchanged") : $"+{offset}";
    }
}
