using Colossal.IO.AssetDatabase;
using CS2Optimization.Core;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;

namespace CS2Optimization
{
    /// <summary>Shadow distances offered in Custom. The numeric value is the distance in metres.</summary>
    public enum ShadowDistanceChoice
    {
        Unchanged = 0,
        Metres100 = 100,
        Metres175 = 175,
        Metres300 = 300,
        Metres500 = 500,
    }

    /// <summary>Prop LOD offsets offered in Custom. Each +6 halves the draw distance.</summary>
    public enum PropDistanceChoice
    {
        Unchanged = 0,
        Half = 6,
        Quarter = 12,
    }

    [FileLocation(nameof(CS2Optimization))]
    [SettingsUIGroupOrder(StatusGroup, ModeGroup, CustomGroup)]
    [SettingsUIShowGroupName(StatusGroup, ModeGroup, CustomGroup)]
    public sealed class Settings : ModSetting
    {
        public const string MainSection = "Main";
        public const string StatusGroup = "Status";
        public const string ModeGroup = "Mode";
        public const string CustomGroup = "Custom";

        public Settings(IMod mod) : base(mod)
        {
        }

        [SettingsUISection(MainSection, ModeGroup)]
        public Mode Mode { get; set; } = SettingsTransition.DefaultMode;

        [SettingsUISection(MainSection, CustomGroup)]
        [SettingsUIDisableByCondition(typeof(Settings), nameof(IsNotCustom))]
        public ShadowDistanceChoice CustomShadowDistance { get; set; } = ShadowDistanceChoice.Metres300;

        [SettingsUISection(MainSection, CustomGroup)]
        [SettingsUIDisableByCondition(typeof(Settings), nameof(IsNotCustom))]
        public PropDistanceChoice CustomPropDistance { get; set; } = PropDistanceChoice.Half;

        /// <summary>What is requested and what is actually applied, or why nothing is.</summary>
        [SettingsUISection(MainSection, StatusGroup)]
        public string Status => Mod.Controller?.Status(IsJapanese()) ?? "-";

        public bool IsNotCustom() => Mode != Mode.Custom;

        public override void SetDefaults()
        {
            Mode = SettingsTransition.DefaultMode;
            CustomShadowDistance = ShadowDistanceChoice.Metres300;
            CustomPropDistance = PropDistanceChoice.Half;
        }

        public override void Apply()
        {
            base.Apply();
            Mod.Controller?.OnSettingsApplied();
        }

        private static bool IsJapanese()
        {
            try
            {
                var locale = GameManager.instance?.localizationManager?.activeLocaleId;
                return locale != null && locale.StartsWith("ja", System.StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
