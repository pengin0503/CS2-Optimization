using System.Collections.Generic;
using Colossal;
using CS2Optimization.Core;

namespace CS2Optimization
{
    public sealed class LocaleEN : IDictionarySource
    {
        private readonly Settings m_Settings;

        public LocaleEN(Settings settings) => m_Settings = settings;

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            var s = m_Settings;
            return new Dictionary<string, string>
            {
                { s.GetSettingsLocaleID(), "CS2 Optimization" },
                { s.GetOptionTabLocaleID(Settings.MainSection), "Main" },
                { s.GetOptionGroupLocaleID(Settings.StatusGroup), "Status" },
                { s.GetOptionGroupLocaleID(Settings.ModeGroup), "Mode" },
                { s.GetOptionGroupLocaleID(Settings.CustomGroup), "Custom values" },

                { s.GetOptionLabelLocaleID(nameof(Settings.Status)), "Current state" },
                { s.GetOptionDescLocaleID(nameof(Settings.Status)), "What is requested, what is actually applied, and why a feature is inactive or stopped." },

                { s.GetOptionLabelLocaleID(nameof(Settings.Mode)), "Mode" },
                {
                    s.GetOptionDescLocaleID(nameof(Settings.Mode)),
                    "Auto: sun shadows limited to 300 m (only if the game's shadows are on) and small props drawn to about half their distance. " +
                    "With Write Everywhere installed, Auto leaves props alone so signs and text do not vanish early. " +
                    "Trees, vehicles, citizens and buildings are never changed. Off: the game's own values. Custom: choose below."
                },
                { s.GetEnumValueLocaleID(Mode.Auto), "Auto (recommended)" },
                { s.GetEnumValueLocaleID(Mode.Off), "Off" },
                { s.GetEnumValueLocaleID(Mode.Custom), "Custom" },

                { s.GetOptionLabelLocaleID(nameof(Settings.CustomShadowDistance)), "Sun shadow distance" },
                {
                    s.GetOptionDescLocaleID(nameof(Settings.CustomShadowDistance)),
                    "Shadows further than this are not drawn. Only applies while the game's own shadows are on, and never extends a distance the game already keeps shorter. Resolution and cascades are not changed."
                },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Unchanged), "Unchanged" },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Metres100), "100 m" },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Metres175), "175 m" },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Metres300), "300 m" },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Metres500), "500 m" },

                { s.GetOptionLabelLocaleID(nameof(Settings.CustomPropDistance)), "Small prop draw distance" },
                {
                    s.GetOptionDescLocaleID(nameof(Settings.CustomPropDistance)),
                    "Street furniture and other small static props stop being drawn sooner. Buildings, trees and plants are excluded. With Write Everywhere, shortening this can make signs and text disappear earlier."
                },
                { s.GetEnumValueLocaleID(PropDistanceChoice.Unchanged), "Unchanged" },
                { s.GetEnumValueLocaleID(PropDistanceChoice.Half), "About half (+6)" },
                { s.GetEnumValueLocaleID(PropDistanceChoice.Quarter), "About a quarter (+12)" },
            };
        }

        public void Unload()
        {
        }
    }
}
