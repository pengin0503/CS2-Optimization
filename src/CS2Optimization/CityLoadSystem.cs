using Colossal.Serialization.Entities;
using Game;

namespace CS2Optimization
{
    /// <summary>Re-evaluates conflicts and the profile once a city (or the menu) has loaded.</summary>
    public partial class CityLoadSystem : GameSystemBase
    {
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            Mod.Controller?.OnCityLoaded();
        }

        protected override void OnUpdate()
        {
        }
    }
}
