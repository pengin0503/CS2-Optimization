using System.Collections.Generic;
using Colossal;
using CS2Optimization.Core;

namespace CS2Optimization
{
    public sealed class LocaleJA : IDictionarySource
    {
        private readonly Settings m_Settings;

        public LocaleJA(Settings settings) => m_Settings = settings;

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            var s = m_Settings;
            return new Dictionary<string, string>
            {
                { s.GetSettingsLocaleID(), "CS2 Optimization" },
                { s.GetOptionTabLocaleID(Settings.MainSection), "メイン" },
                { s.GetOptionGroupLocaleID(Settings.StatusGroup), "状態" },
                { s.GetOptionGroupLocaleID(Settings.ModeGroup), "モード" },
                { s.GetOptionGroupLocaleID(Settings.CustomGroup), "カスタム値" },

                { s.GetOptionLabelLocaleID(nameof(Settings.Status)), "現在の状態" },
                { s.GetOptionDescLocaleID(nameof(Settings.Status)), "要求値と実際に適用された値、機能が無効・停止している理由を表示します。" },

                { s.GetOptionLabelLocaleID(nameof(Settings.Mode)), "モード" },
                {
                    s.GetOptionDescLocaleID(nameof(Settings.Mode)),
                    "Auto: 太陽光の影の描画距離を300 mに制限（ゲーム設定で影が有効な場合のみ）し、小物の描画距離をおよそ半分にします。" +
                    "Write Everywhere 導入時は看板や文字の早期消失を避けるため、Autoでは小物を変更しません。" +
                    "木・車両・市民・建物は変更しません。Off: ゲームの値のまま。Custom: 下で個別に選択。"
                },
                { s.GetEnumValueLocaleID(Mode.Auto), "Auto（推奨）" },
                { s.GetEnumValueLocaleID(Mode.Off), "Off" },
                { s.GetEnumValueLocaleID(Mode.Custom), "Custom" },

                { s.GetOptionLabelLocaleID(nameof(Settings.CustomShadowDistance)), "影の描画距離" },
                {
                    s.GetOptionDescLocaleID(nameof(Settings.CustomShadowDistance)),
                    "これより遠い影を描画しません。ゲーム設定で影が有効なときだけ適用し、ゲームがより短い距離にしている場合は延ばしません。解像度やカスケード数は変更しません。"
                },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Unchanged), "無変更" },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Metres100), "100 m" },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Metres175), "175 m" },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Metres300), "300 m" },
                { s.GetEnumValueLocaleID(ShadowDistanceChoice.Metres500), "500 m" },

                { s.GetOptionLabelLocaleID(nameof(Settings.CustomPropDistance)), "小物の描画距離" },
                {
                    s.GetOptionDescLocaleID(nameof(Settings.CustomPropDistance)),
                    "街路設備などの小さな静的オブジェクトを早めに非表示にします。建物・木・植物は対象外です。Write Everywhere 併用時は看板や文字が早く消える場合があります。"
                },
                { s.GetEnumValueLocaleID(PropDistanceChoice.Unchanged), "無変更" },
                { s.GetEnumValueLocaleID(PropDistanceChoice.Half), "約半分（+6）" },
                { s.GetEnumValueLocaleID(PropDistanceChoice.Quarter), "約1/4（+12）" },
            };
        }

        public void Unload()
        {
        }
    }
}
