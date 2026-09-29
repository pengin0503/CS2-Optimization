// Portions adapted from Cs2Saver (CS2PerformancePatcher, commit 69015dd90cfc667c100beb16fd158a43a9e03a23),
// Copyright (C) 2026 NSPX. Licensed under GPL-3.0; see LICENSE and THIRD_PARTY_NOTICES.md.

using System;
using Colossal.Serialization.Entities;
using CS2Optimization.Core;
using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace CS2Optimization.Rendering
{
    /// <summary>
    /// Raises the LOD floor (<c>ObjectGeometryData.m_MinLod</c>) of small static and net props so
    /// they stop being drawn sooner. Each +6 halves the distance at which they disappear
    /// (<c>RenderingUtils.CalculateDistanceFactor</c> is <c>2^((128 - lod) / 6)</c>). Buildings,
    /// trees and plants are never touched, see <see cref="PrefabCategoryPolicy"/>.
    ///
    /// <para><b>When it becomes visible.</b> Static objects are culled through the object search
    /// tree, which copies the prefab's floor when an object is inserted
    /// (<c>Game.Objects.SearchSystem.UpdateSearchTreeJob</c>); objects are only re-inserted when
    /// they are created, updated or the city is loaded. So the prefab value set here applies to
    /// every city loaded afterwards and to newly placed props immediately, while props already
    /// placed keep their indexed value until the city is reloaded. Forcing a re-index would mean
    /// marking every prop <c>Updated</c>, which wakes many unrelated game systems, so it is not
    /// done; the status line says a reload is pending instead.</para>
    ///
    /// <para><b>Originals.</b> Every prefab's own value is kept in a <see cref="LodLedger{TId}"/>,
    /// so passes never stack and Off, failure and mod unload put the game's values back. The
    /// ledger is not cleared between cities: restoring before a load could leave the next city
    /// indexed with the original values. Stale entries are safe anyway, because a destroyed
    /// entity no longer <c>Exists</c> (its version changes on reuse) and a value that no longer
    /// matches what was applied is recaptured as the new original. <c>m_MinLod</c> is not part of
    /// <c>ObjectGeometryData.Serialize</c>, so nothing here reaches a save file.</para>
    /// </summary>
    public partial class PrefabLodSystem : GameSystemBase
    {
        private readonly LodLedger<Entity> m_Ledger = new LodLedger<Entity>();
        private readonly WarnOnce m_Warn = new WarnOnce();

        /// <summary>Prefabs that might be props. Final eligibility is decided per entity by the policy.</summary>
        private EntityQuery m_Candidates;

        private int m_Offset;
        private int m_AppliedOffset;
        private bool m_Dirty = true;
        private int m_LastCandidateCount = -1;
        private bool m_Failed;
        private bool m_InCity;
        private int m_OffsetAtCityLoad;

        /// <summary>Offset in effect on the prefabs this system tracks. 0 = game values.</summary>
        public int AppliedOffset => m_Failed ? 0 : m_AppliedOffset;

        /// <summary>
        /// The offset changed since the current city loaded, so props already placed still use
        /// the value they were indexed with.
        /// </summary>
        public bool ReloadPending => m_InCity && AppliedOffset != m_OffsetAtCityLoad;

        /// <summary>True once the feature has stood down after an error.</summary>
        public bool Failed => m_Failed;

        /// <summary>Called on the main thread by the controller. Takes effect immediately.</summary>
        public void SetOffset(int offset)
        {
            if (offset == m_Offset && !m_Dirty) return;
            m_Offset = offset;
            m_Dirty = true;
            RunPass("setting change");
        }

        /// <summary>Puts every tracked prefab back to its original floor, synchronously.</summary>
        public void RestoreNow() => Restore();

        protected override void OnCreate()
        {
            base.OnCreate();

            m_Candidates = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadWrite<ObjectGeometryData>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<StaticObjectData>(),
                    ComponentType.ReadOnly<NetObjectData>(),
                },
            });
        }

        protected override void OnUpdate()
        {
            if (m_Failed) return;

            // New prefabs appear while a city loads; counting them costs one integer per chunk
            // and is enough to notice without sweeping anything.
            var count = m_Candidates.CalculateEntityCount();
            if (!m_Dirty && count == m_LastCandidateCount) return;

            m_LastCandidateCount = count;
            RunPass("new prefabs");
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            // Catch any prefab the load created that the count check has not seen yet, then note
            // the value this city's props were indexed with.
            RunPass("city load");
            m_InCity = (mode & GameMode.Game) != 0;
            m_OffsetAtCityLoad = AppliedOffset;
        }

        protected override void OnDestroy()
        {
            Guard("destroy", RestoreNow);
            base.OnDestroy();
        }

        private void RunPass(string why)
        {
            if (m_Failed) return;
            m_Dirty = false;
            Guard(why, Apply);
        }

        private void Apply()
        {
            if (m_Offset == 0)
            {
                Restore();
                return;
            }

            var changed = 0;
            var eligible = 0;
            var entities = m_Candidates.ToEntityArray(Allocator.Temp);
            var geometry = m_Candidates.ToComponentDataArray<ObjectGeometryData>(Allocator.Temp);

            try
            {
                for (var i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    if (!IsEligible(entity)) continue;
                    eligible++;

                    var data = geometry[i];
                    if (!m_Ledger.Plan(entity, data.m_MinLod, m_Offset, out var write)) continue;

                    changed++;
                    data.m_MinLod = write;
                    EntityManager.SetComponentData(entity, data);
                }
            }
            finally
            {
                geometry.Dispose();
                entities.Dispose();
            }

            m_AppliedOffset = m_Offset;
            if (changed > 0) Mod.Log.Info($"Prop LOD +{m_Offset}: {eligible} eligible prefabs, {changed} updated.");
        }

        private void Restore()
        {
            if (m_Ledger.Count == 0)
            {
                m_AppliedOffset = 0;
                return;
            }

            var em = EntityManager;

            var result = m_Ledger.RestoreAll(
                entity => em.Exists(entity) && em.HasComponent<ObjectGeometryData>(entity)
                    ? em.GetComponentData<ObjectGeometryData>(entity).m_MinLod
                    : (int?)null,
                (entity, original) =>
                {
                    var data = em.GetComponentData<ObjectGeometryData>(entity);
                    data.m_MinLod = original;
                    em.SetComponentData(entity, data);
                });

            m_AppliedOffset = 0;

            // Anything not written back is recorded rather than forced: a missing prefab has
            // nothing to restore, and a value changed since belongs to whoever changed it.
            if (result.Missing > 0 || result.ChangedElsewhere > 0)
            {
                Mod.Log.Warn($"Prop LOD restore skipped {result.Missing} prefabs that no longer exist " +
                             $"and {result.ChangedElsewhere} whose value was changed by something else.");
            }

            if (result.Restored > 0) Mod.Log.Info($"Prop LOD restored: {result.Restored} prefabs.");
        }

        private bool IsEligible(Entity prefab)
        {
            var em = EntityManager;
            return PrefabCategoryPolicy.IsEligible(
                em.HasComponent<StaticObjectData>(prefab),
                em.HasComponent<NetObjectData>(prefab),
                em.HasComponent<BuildingData>(prefab),
                em.HasComponent<TreeData>(prefab),
                em.HasComponent<PlantData>(prefab));
        }

        private void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                if (m_Warn.ShouldLog(what)) Mod.Log.Error(ex, $"Prop LOD failed during {what}; restoring and standing down.");
                m_Failed = true;

                // Put back whatever can still be put back from the originals already captured.
                try
                {
                    Restore();
                }
                catch (Exception restoreEx)
                {
                    if (m_Warn.ShouldLog("restore-after-failure")) Mod.Log.Error(restoreEx, "Prop LOD restore after failure also failed.");
                }
            }
        }
    }
}
