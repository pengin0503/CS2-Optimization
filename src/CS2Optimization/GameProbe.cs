using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CS2Optimization.Core;
using CS2Optimization.Rendering;
using Game.Prefabs;
using Game.Settings;
using UnityEngine.Rendering.HighDefinition;

namespace CS2Optimization
{
    /// <summary>Reads what the running game looks like. Every probe fails soft.</summary>
    internal static class GameProbe
    {
        private static readonly WarnOnce s_Warn = new WarnOnce();

        /// <summary>The game's own version string, e.g. "1.6.2f1" (what the game logs at startup).</summary>
        public static string GameVersion() => Guarded("version", ReadGameVersion, string.Empty);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadGameVersion() => Game.Version.current.shortVersion ?? string.Empty;

        public static ModPresence DetectMods() =>
            Guarded("assemblies", () => ModPresence.Detect(AppDomain.CurrentDomain.GetAssemblies().Select(SafeName)), default);

        private static string? SafeName(Assembly assembly)
        {
            try
            {
                return assembly.GetName().Name;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The prefab field this mod writes still has the shape it was built against.</summary>
        public static bool PropFieldsCompatible() => Guarded("prop fields", CheckPropFields, false);

        /// <summary>The game still keeps its per-frame shadow values where this mod reads them.</summary>
        public static bool ShadowFieldsCompatible() => Guarded("shadow fields", CheckShadowFields, false);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CheckPropFields() =>
            typeof(ObjectGeometryData).GetField("m_MinLod")?.FieldType == typeof(int);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CheckShadowFields() =>
            ShadowDistanceSystem.GameShadowField?.FieldType == typeof(HDShadowSettings);

        /// <summary>The game's own shadow switch (Options → Graphics); null when not available yet.</summary>
        public static bool? GameShadowsEnabled() => Guarded<bool?>("shadow setting", ReadShadowSetting, null);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool? ReadShadowSetting() =>
            SharedSettings.instance?.graphics?.GetQualitySetting<ShadowsQualitySettings>()?.enabled;

        private static T Guarded<T>(string what, Func<T> probe, T fallback)
        {
            try
            {
                return probe();
            }
            catch (Exception ex)
            {
                if (s_Warn.ShouldLog(what)) Mod.Log.Warn($"Probe '{what}' failed: {ex.GetType().Name}: {ex.Message}");
                return fallback;
            }
        }
    }
}
