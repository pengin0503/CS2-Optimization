// Portions adapted from Cs2Saver (CS2PerformancePatcher, commit 69015dd90cfc667c100beb16fd158a43a9e03a23),
// Copyright (C) 2026 NSPX. Licensed under GPL-3.0; see LICENSE and THIRD_PARTY_NOTICES.md.

using System;
using System.Reflection;
using CS2Optimization.Core;
using Game;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace CS2Optimization.Rendering
{
    /// <summary>
    /// Caps how far the sun's shadows reach, through one HDRP <see cref="Volume"/> this system
    /// owns, without ever lengthening them.
    ///
    /// <para><b>Why every frame.</b> The game drives <c>maxShadowDistance</c> and the cascade
    /// split ratios itself, every frame, from camera height and pitch
    /// (<c>CameraUpdateSystem.UpdateShadowsCascades</c>, 0 m to 14 000 m). A fixed override would
    /// lengthen close-up shadows and squash the game's cascades. So this runs right after
    /// <c>CameraUpdateSystem</c>, reads what it just chose (read only; the game's volume is never
    /// written) and, only while that exceeds the limit, overrides the distance and re-places the
    /// splits via <see cref="ShadowCascadeClamp"/>. Otherwise the override is inactive and the
    /// game's values pass through. Cascade count, borders, resolution and the sun light are never
    /// touched, and shadows are never switched on.</para>
    /// </summary>
    public partial class ShadowDistanceSystem : GameSystemBase
    {
        /// <summary>Frames between reads of the game's shadow on/off setting.</summary>
        public const int RecheckFrames = 120;

        /// <summary>Above the game's own volumes (camera 51, quality 100, overrides 2000, water 5000).</summary>
        private const float OverridePriority = 10_000f;

        /// <summary>The volume component CameraUpdateSystem writes its per-frame shadow values into.</summary>
        internal static readonly FieldInfo? GameShadowField =
            typeof(CameraUpdateSystem).GetField("m_ShadowSettings", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly WarnOnce m_Warn = new WarnOnce();

        private CameraUpdateSystem? m_Camera;
        private HDShadowSettings? m_GameShadow;

        private GameObject? m_Holder;
        private VolumeProfile? m_Profile;
        private HDShadowSettings? m_Shadow;

        private int m_Request;
        private int m_Countdown;
        private bool m_Failed;

        /// <summary>Reads the game's shadow switch; null when unknown. Set by the mod.</summary>
        public Func<bool?> ShadowProbe { get; set; } = () => null;

        /// <summary>The cap enforced this frame, or null while the game's own distance is in effect.</summary>
        public float? AppliedMetres { get; private set; }

        /// <summary>The distance the game chose this frame, when it could be read.</summary>
        public float? GameMetres { get; private set; }

        /// <summary>The game's shadow switch as last read; null when unknown.</summary>
        public bool? GameShadowsEnabled { get; private set; }

        public bool Failed => m_Failed;

        /// <summary>Called on the main thread by the controller. 0 removes the override at once.</summary>
        public void SetRequest(int metres)
        {
            m_Request = metres;
            m_Countdown = 0;
            if (metres == 0) RestoreNow();
        }

        /// <summary>Removes the override synchronously and releases the volume.</summary>
        public void RestoreNow()
        {
            if (m_Shadow != null) m_Shadow.active = false;

            if (m_Shadow != null) UnityEngine.Object.Destroy(m_Shadow);
            if (m_Profile != null) UnityEngine.Object.Destroy(m_Profile);
            if (m_Holder != null) UnityEngine.Object.Destroy(m_Holder);

            m_Shadow = null;
            m_Profile = null;
            m_Holder = null;
            AppliedMetres = null;
        }

        protected override void OnUpdate()
        {
            if (m_Failed) return;

            try
            {
                if (--m_Countdown <= 0)
                {
                    m_Countdown = RecheckFrames;
                    GameShadowsEnabled = ShadowProbe();
                }

                var limit = ShadowOverrideState.Resolve(m_Request, GameShadowsEnabled, null);
                var game = limit.HasValue ? GameShadow() : null;

                // No request, shadows off/unknown, or the game is not steering the distance right
                // now (then there is nothing trustworthy to clamp): the game's values pass through.
                if (!limit.HasValue || game == null || !game.maxShadowDistance.overrideState)
                {
                    GameMetres = null;
                    Suspend();
                    return;
                }

                var distance = game.maxShadowDistance.value;
                GameMetres = distance;

                if (ShadowCascadeClamp.TryClamp(
                        distance,
                        game.cascadeShadowSplit0.value,
                        game.cascadeShadowSplit1.value,
                        game.cascadeShadowSplit2.value,
                        limit.Value,
                        out var cascades))
                {
                    Apply(cascades);
                }
                else
                {
                    Suspend();
                }
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

        private HDShadowSettings? GameShadow()
        {
            if (m_GameShadow != null) return m_GameShadow;

            m_Camera ??= World.GetExistingSystemManaged<CameraUpdateSystem>();
            if (m_Camera == null || GameShadowField == null) return null;

            m_GameShadow = GameShadowField.GetValue(m_Camera) as HDShadowSettings;
            if (m_GameShadow == null && m_Warn.ShouldLog("game-shadow"))
                Mod.Log.Info("Game shadow settings not available yet; shadow limit waits.");
            return m_GameShadow;
        }

        private void Apply(ShadowCascades cascades)
        {
            if (m_Shadow == null) Build();

            m_Shadow!.maxShadowDistance.Override(cascades.Distance);
            m_Shadow.cascadeShadowSplit0.Override(cascades.Split0);
            m_Shadow.cascadeShadowSplit1.Override(cascades.Split1);
            m_Shadow.cascadeShadowSplit2.Override(cascades.Split2);
            m_Shadow.active = true;

            if (!AppliedMetres.HasValue && m_Warn.ShouldLog("engaged"))
                Mod.Log.Info($"Shadow limit engaged: {cascades.Distance:0} m (game wanted {GameMetres:0} m).");
            AppliedMetres = cascades.Distance;
        }

        /// <summary>Lets the game's values through without releasing the volume (cheap to toggle per frame).</summary>
        private void Suspend()
        {
            if (m_Shadow != null) m_Shadow.active = false;
            AppliedMetres = null;
        }

        private void Build()
        {
            m_Holder = new GameObject("CS2Optimization Shadow Limit") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(m_Holder);

            // Volumes are global by default, which is how the game creates its own.
            var volume = m_Holder.AddComponent<Volume>();
            volume.priority = OverridePriority;
            volume.weight = 1f;

            m_Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_Profile.hideFlags = HideFlags.HideAndDontSave;
            volume.sharedProfile = m_Profile;

            // overrides: false leaves every parameter unset; only those set in Apply() take part.
            m_Shadow = m_Profile.Add<HDShadowSettings>(false);
        }
    }
}
