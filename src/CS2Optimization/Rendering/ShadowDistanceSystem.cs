// Portions adapted from Cs2Saver (CS2PerformancePatcher, commit 69015dd90cfc667c100beb16fd158a43a9e03a23),
// Copyright (C) 2026 NSPX. Licensed under GPL-3.0; see LICENSE and THIRD_PARTY_NOTICES.md.

using System;
using CS2Optimization.Core;
using Game;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CS2Optimization.Rendering
{
    /// <summary>
    /// Limits how far from the camera the sun's shadows are drawn, through a global HDRP
    /// <see cref="Volume"/> this system creates and owns. Only
    /// <see cref="HDShadowSettings.maxShadowDistance"/> is overridden: cascade count, splits,
    /// resolution, the sun light and the game's own volumes are never written, and nothing here
    /// turns shadows on. Everything outside the radius stops being rasterised into the shadow
    /// map, which is where the saving comes from.
    ///
    /// <para>The game's shadow switch is re-read every <see cref="RecheckFrames"/> frames, so the
    /// override follows the options page. Turning the feature off disables and destroys the
    /// volume immediately, which hands the game's own distance straight back.</para>
    /// </summary>
    public partial class ShadowDistanceSystem : GameSystemBase
    {
        /// <summary>Two seconds at 60 fps: prompt enough to follow the options page, costs nothing.</summary>
        public const int RecheckFrames = 120;

        /// <summary>Above the game's own volumes, so this single parameter wins wherever the camera is.</summary>
        private const float OverridePriority = 10_000f;

        private readonly WarnOnce m_Warn = new WarnOnce();

        private GameObject? m_Holder;
        private Volume? m_Volume;
        private VolumeProfile? m_Profile;
        private HDShadowSettings? m_Shadow;

        private int m_Request;
        private bool m_Dirty = true;
        private int m_Countdown;
        private bool m_Failed;

        /// <summary>The distance in effect without this override, last time it could be read.</summary>
        private float? m_Baseline;
        private bool m_ActiveAtLastCheck;

        /// <summary>Reads the game's shadow switch; null when unknown. Set by the mod.</summary>
        public Func<bool?> ShadowProbe { get; set; } = () => null;

        /// <summary>Distance currently overridden, or null when the game's own distance is in effect.</summary>
        public float? AppliedMetres { get; private set; }

        /// <summary>The game's shadow switch as last read; null when unknown.</summary>
        public bool? GameShadowsEnabled { get; private set; }

        public bool Failed => m_Failed;

        /// <summary>Called on the main thread by the controller. Takes effect immediately.</summary>
        public void SetRequest(int metres)
        {
            if (metres == m_Request && !m_Dirty) return;
            m_Request = metres;
            m_Dirty = true;
            Check();
        }

        /// <summary>Removes the override synchronously and releases the volume.</summary>
        public void RestoreNow()
        {
            if (m_Volume != null) m_Volume.enabled = false;
            if (m_Shadow != null) m_Shadow.active = false;

            if (m_Shadow != null) UnityEngine.Object.Destroy(m_Shadow);
            if (m_Profile != null) UnityEngine.Object.Destroy(m_Profile);
            if (m_Holder != null) UnityEngine.Object.Destroy(m_Holder);

            m_Profile = null;
            m_Shadow = null;
            m_Volume = null;
            m_Holder = null;

            if (AppliedMetres.HasValue) Mod.Log.Info("Shadow distance override removed.");
            AppliedMetres = null;
        }

        protected override void OnUpdate()
        {
            if (m_Failed) return;
            if (!m_Dirty && --m_Countdown > 0) return;
            Check();
        }

        protected override void OnDestroy()
        {
            try
            {
                RestoreNow();
            }
            catch (Exception ex)
            {
                Mod.Log.Warn($"Shadow distance cleanup failed: {ex.Message}");
            }

            base.OnDestroy();
        }

        private void Check()
        {
            if (m_Failed) return;
            m_Dirty = false;
            m_Countdown = RecheckFrames;

            try
            {
                GameShadowsEnabled = ShadowProbe();

                // The volume stack only shows the game's own distance while this override has
                // been off since the previous check; otherwise it may still hold ours.
                if (!AppliedMetres.HasValue && !m_ActiveAtLastCheck) ReadBaseline();

                var target = ShadowOverrideState.Resolve(m_Request, GameShadowsEnabled, m_Baseline);
                if (target != AppliedMetres)
                {
                    if (target.HasValue) Enable(target.Value);
                    else RestoreNow();
                }

                m_ActiveAtLastCheck = AppliedMetres.HasValue;
            }
            catch (Exception ex)
            {
                m_Failed = true;
                Mod.Log.Error(ex, "Shadow distance control failed; removing the override and standing down.");
                try
                {
                    RestoreNow();
                }
                catch (Exception restoreEx)
                {
                    Mod.Log.Error(restoreEx, "Removing the shadow override after a failure also failed.");
                }
            }
        }

        private void Enable(float metres)
        {
            if (m_Volume == null || m_Shadow == null) Build();

            m_Shadow!.maxShadowDistance.Override(metres);
            m_Shadow.active = true;
            m_Volume!.enabled = true;

            AppliedMetres = metres;
            Mod.Log.Info($"Shadow distance limited to {metres:0} m (game value before override: {(m_Baseline.HasValue ? $"{m_Baseline.Value:0} m" : "unknown")}).");
        }

        private void Build()
        {
            m_Holder = new GameObject("CS2Optimization Shadow Distance") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(m_Holder);

            m_Volume = m_Holder.AddComponent<Volume>();
            m_Volume.isGlobal = true;
            m_Volume.priority = OverridePriority;
            m_Volume.weight = 1f;

            m_Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_Profile.hideFlags = HideFlags.HideAndDontSave;
            m_Volume.sharedProfile = m_Profile;

            // overrides: false leaves every parameter unset, so only the one set in Enable()
            // takes part in blending and the game's cascades and resolution come through.
            m_Shadow = m_Profile.Add<HDShadowSettings>(false);
        }

        private void ReadBaseline()
        {
            try
            {
                var stack = VolumeManager.instance.stack;
                var settings = stack?.GetComponent<HDShadowSettings>();
                if (settings == null) return;

                var value = settings.maxShadowDistance.value;
                if (m_Baseline != value && m_Warn.ShouldLog($"baseline-{value:0}"))
                    Mod.Log.Info($"Game shadow distance read as {value:0} m.");
                m_Baseline = value;
            }
            catch (Exception ex)
            {
                if (m_Warn.ShouldLog("baseline")) Mod.Log.Warn($"Could not read the game's shadow distance: {ex.Message}");
            }
        }
    }
}
