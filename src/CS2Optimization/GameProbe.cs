using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using CS2Optimization.Core;
using Game.Prefabs;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace CS2Optimization
{
    /// <summary>Reads what the running game looks like. Every probe fails soft.</summary>
    internal static class GameProbe
    {
        private static readonly WarnOnce s_Warn = new WarnOnce();

        public static string GameVersion()
        {
            try
            {
                return Application.version ?? string.Empty;
            }
            catch (Exception ex)
            {
                if (s_Warn.ShouldLog("version")) Mod.Log.Warn($"Could not read the game version: {ex.Message}");
                return string.Empty;
            }
        }

        public static ModPresence DetectMods()
        {
            try
            {
                return ModPresence.Detect(AppDomain.CurrentDomain.GetAssemblies().Select(SafeName));
            }
            catch (Exception ex)
            {
                if (s_Warn.ShouldLog("assemblies")) Mod.Log.Warn($"Could not list loaded assemblies: {ex.Message}");
                return default;
            }
        }

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

        /// <summary>
        /// Checks that the fields this mod writes still have the shape it was built against.
        /// Called inside try/catch: on a different game build the type itself may fail to load.
        /// </summary>
        public static bool PropFieldsCompatible()
        {
            return Guarded("prop fields", CheckPropFields);
        }

        public static bool ShadowFieldsCompatible()
        {
            return Guarded("shadow fields", CheckShadowFields);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CheckPropFields() =>
            typeof(ObjectGeometryData).GetField("m_MinLod")?.FieldType == typeof(int)
            && typeof(CullingInfo).GetField("m_MinLod")?.FieldType == typeof(byte);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool CheckShadowFields() =>
            typeof(HDShadowSettings).GetField("maxShadowDistance") != null;

        /// <summary>
        /// The game's own shadow switch: true/false, or null when it cannot be determined.
        ///
        /// <para>Read through reflection on purpose. The settings type is not part of the
        /// verified surface of this build, and a mismatch must switch the shadow feature off
        /// with one log line rather than stop the mod from loading. Falls back to the sun
        /// light, which the game leaves without shadows while its shadow setting is off.</para>
        /// </summary>
        public static bool? GameShadowsEnabled()
        {
            var fromSettings = Guarded<bool?>("shadow setting", ReadShadowSetting, null);
            if (fromSettings.HasValue) return fromSettings;
            return Guarded<bool?>("sun light", ReadSunLight, null);
        }

        private static bool? ReadShadowSetting()
        {
            var sharedType = Type.GetType("Game.Settings.SharedSettings, Game");
            var shadowsType = Type.GetType("Game.Settings.ShadowsQualitySettings, Game");
            if (sharedType == null || shadowsType == null) return Missing("settings types");

            if (!TryGetMember(sharedType, null, "instance", out var shared)) return Missing("SharedSettings.instance");
            if (shared == null) return null; // settings not loaded yet; not an error

            if (!TryGetMember(shared.GetType(), shared, "graphics", out var graphics)) return Missing("SharedSettings.graphics");
            if (graphics == null) return null;

            var getter = graphics.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetQualitySetting" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            if (getter == null) return Missing("GraphicsSettings.GetQualitySetting<T>()");

            var shadows = getter.MakeGenericMethod(shadowsType).Invoke(graphics, null);
            if (shadows == null) return null;

            if (!TryGetMember(shadowsType, shadows, "enabled", out var value)) return Missing("ShadowsQualitySettings.enabled");
            return value is bool enabled ? enabled : Missing("ShadowsQualitySettings.enabled (not a bool)");
        }

        /// <summary>Public property or field, static when <paramref name="target"/> is null.</summary>
        private static bool TryGetMember(Type type, object? target, string name, out object? value)
        {
            var flags = BindingFlags.Public | (target == null ? BindingFlags.Static : BindingFlags.Instance);
            var property = type.GetProperty(name, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                value = property.GetValue(target);
                return true;
            }

            var field = type.GetField(name, flags);
            if (field != null)
            {
                value = field.GetValue(target);
                return true;
            }

            value = null;
            return false;
        }

        private static bool? ReadSunLight()
        {
            var sun = GameObject.FindWithTag("SunLight");
            var light = sun != null ? sun.GetComponent<Light>() : null;
            if (light == null) return null;
            return light.shadows != LightShadows.None;
        }

        private static bool? Missing(string what)
        {
            if (s_Warn.ShouldLog("missing-" + what)) Mod.Log.Warn($"Game shadow setting: {what} not found; falling back to the sun light.");
            return null;
        }

        private static bool Guarded(string what, Func<bool> probe) => Guarded(what, probe, false);

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
