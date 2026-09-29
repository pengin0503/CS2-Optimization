using System;
using CS2Optimization.Core;
using CS2Optimization.Rendering;

namespace CS2Optimization
{
    /// <summary>
    /// Connects the saved settings and the runtime environment to the two controllers.
    /// Re-evaluated when settings are applied and when a city finishes loading.
    /// </summary>
    internal sealed class OptimizationController
    {
        private readonly Settings m_Settings;
        private readonly PrefabLodSystem? m_Props;
        private readonly ShadowDistanceSystem? m_Shadows;

        private Mode? m_LastMode;
        private EffectiveProfile m_Profile;
        private ModPresence m_Presence;
        private string m_GameVersion = string.Empty;
        private bool m_SupportedGame;
        private bool m_PropFieldsOk;
        private bool m_ShadowFieldsOk;
        private bool m_Applying;
        private string? m_LastLogged;

        public OptimizationController(Settings settings, PrefabLodSystem? props, ShadowDistanceSystem? shadows)
        {
            m_Settings = settings;
            m_Props = props;
            m_Shadows = shadows;
        }

        /// <summary>First evaluation after the saved settings are loaded. Never rewrites them.</summary>
        public void Initialize()
        {
            DetectEnvironment();
            Apply();
        }

        /// <summary>Called by <see cref="Settings.Apply"/> whenever the player changes an option.</summary>
        public void OnSettingsApplied() => Apply();

        /// <summary>Mods can finish loading after this one; look again once a city is up.</summary>
        public void OnCityLoaded()
        {
            DetectEnvironment();
            Apply();
        }

        public string Status(bool japanese)
        {
            var state = new StatusState
            {
                StopReason = m_Profile.StopReason,
                GameVersion = m_GameVersion,
                WriteEverywhere = m_Presence.WriteEverywhere,
                GameShadowsEnabled = m_Shadows?.GameShadowsEnabled,
                RequestedShadowMetres = m_Profile.RequestedShadowMetres,
                AppliedShadowMetres = m_Shadows?.AppliedMetres,
                GameShadowMetres = m_Shadows?.GameMetres,
                PropReloadPending = m_Props?.ReloadPending ?? false,
                RequestedPropLodOffset = m_Profile.RequestedPropLodOffset,
                AppliedPropLodOffset = m_Props?.AppliedOffset ?? 0,
                ShadowFeatureFailed = m_Shadows == null || m_Shadows.Failed || !m_ShadowFieldsOk,
                PropFeatureFailed = m_Props == null || m_Props.Failed || !m_PropFieldsOk,
            };

            return StatusText.Describe(state, japanese);
        }

        private void DetectEnvironment()
        {
            m_GameVersion = GameProbe.GameVersion();
            m_SupportedGame = GameVersionPolicy.IsSupported(m_GameVersion);
            m_Presence = GameProbe.DetectMods();
            m_PropFieldsOk = GameProbe.PropFieldsCompatible();
            m_ShadowFieldsOk = GameProbe.ShadowFieldsCompatible();
        }

        private void Apply()
        {
            // Seeding Custom saves the settings, which applies them again; that inner call has
            // nothing left to do.
            if (m_Applying) return;
            m_Applying = true;

            try
            {
                var mode = m_Settings.Mode;
                var transition = SettingsTransition.OnApply(
                    m_LastMode, mode, m_Profile,
                    (int)m_Settings.CustomShadowDistance, (int)m_Settings.CustomPropDistance);

                if (transition.CopiedFromPrevious)
                {
                    m_Settings.CustomShadowDistance = (ShadowDistanceChoice)transition.CustomShadowMetres;
                    m_Settings.CustomPropDistance = (PropDistanceChoice)transition.CustomPropLodOffset;
                    m_Settings.ApplyAndSave();
                }

                m_LastMode = mode;

                m_Profile = EffectiveProfilePolicy.Resolve(
                    mode,
                    (int)m_Settings.CustomShadowDistance,
                    (int)m_Settings.CustomPropDistance,
                    m_Presence.WriteEverywhere,
                    m_Presence.Cs2Saver,
                    m_SupportedGame,
                    // The shadow controller re-checks the game's switch itself; this is only for
                    // the profile's own "applied" value.
                    m_Shadows?.GameShadowsEnabled ?? GameProbe.GameShadowsEnabled() ?? false);

                m_Props?.SetOffset(m_PropFieldsOk ? m_Profile.PropLodOffset : 0);
                m_Shadows?.SetRequest(m_ShadowFieldsOk ? m_Profile.ShadowRequestWhenShadowsOn : 0);

                LogIfChanged();
            }
            catch (Exception ex)
            {
                Mod.Log.Error(ex, "Could not apply settings.");
            }
            finally
            {
                m_Applying = false;
            }
        }

        private void LogIfChanged()
        {
            var line = $"Mode={m_Settings.Mode} game={m_GameVersion} supported={m_SupportedGame} " +
                       $"WE={m_Presence.WriteEverywhere} Cs2Saver={m_Presence.Cs2Saver} stop={m_Profile.StopReason} " +
                       $"shadowRequest={m_Profile.ShadowRequestWhenShadowsOn} propOffset={m_Profile.PropLodOffset} " +
                       $"propFields={m_PropFieldsOk} shadowFields={m_ShadowFieldsOk}";
            if (line == m_LastLogged) return;
            m_LastLogged = line;

            if (m_Profile.StopReason == StopReason.None) Mod.Log.Info(line);
            else Mod.Log.Warn(line);
        }
    }
}
