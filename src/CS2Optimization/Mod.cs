using System;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using CS2Optimization.Rendering;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;

namespace CS2Optimization
{
    /// <summary>
    /// Entry point. Each part registers on its own: a failure in one is logged and the rest keep
    /// working, and a mod that stops the game from starting would be worse than no mod.
    /// </summary>
    public sealed class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger(nameof(CS2Optimization)).SetShowsErrorsInUI(false);

        internal static OptimizationController? Controller;

        private PrefabLodSystem? m_Props;
        private ShadowDistanceSystem? m_Shadows;
        private Settings? m_Settings;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info($"{nameof(CS2Optimization)} loading.");

            TryRegister("prop LOD", () =>
            {
                // After ObjectInitializeSystem, which derives m_MinLod in the first place;
                // running before it would have the value overwritten.
                updateSystem.UpdateAfter<PrefabLodSystem, ObjectInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
                m_Props = updateSystem.World.GetOrCreateSystemManaged<PrefabLodSystem>();
            });

            TryRegister("shadow distance", () =>
            {
                // LateUpdate runs every rendered frame, paused or not, so the options page is
                // followed even while the simulation is stopped.
                updateSystem.UpdateAt<ShadowDistanceSystem>(SystemUpdatePhase.LateUpdate);
                m_Shadows = updateSystem.World.GetOrCreateSystemManaged<ShadowDistanceSystem>();
                m_Shadows.ShadowProbe = GameProbe.GameShadowsEnabled;
            });

            TryRegister("city load hook", () =>
            {
                updateSystem.UpdateAt<CityLoadSystem>(SystemUpdatePhase.LateUpdate);
                updateSystem.World.GetOrCreateSystemManaged<CityLoadSystem>();
            });

            // Last, so the systems it drives already exist.
            TryRegister("settings", () =>
            {
                m_Settings = new Settings(this);
                m_Settings.RegisterInOptionsUI();
                GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Settings));
                GameManager.instance.localizationManager.AddSource("ja-JP", new LocaleJA(m_Settings));

                // Saved values win; defaults (Auto) only apply on a fresh install.
                AssetDatabase.global.LoadSettings(nameof(CS2Optimization), m_Settings, new Settings(this));

                Controller = new OptimizationController(m_Settings, m_Props, m_Shadows);
                Controller.Initialize();
            });
        }

        public void OnDispose()
        {
            Controller = null;

            // Synchronous: the game gets its own values back before this returns.
            Try("restore prop LOD", () => m_Props?.RestoreNow());
            Try("remove shadow override", () => m_Shadows?.RestoreNow());
            Try("unregister settings", () => m_Settings?.UnregisterInOptionsUI());

            m_Props = null;
            m_Shadows = null;
            m_Settings = null;
            Log.Info($"{nameof(CS2Optimization)} unloaded.");
        }

        private static void TryRegister(string what, Action register)
        {
            try
            {
                register();
                Log.Info($"Registered {what}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to register {what}; continuing without it.");
            }
        }

        private static void Try(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not {what}: {ex.Message}");
            }
        }
    }
}
