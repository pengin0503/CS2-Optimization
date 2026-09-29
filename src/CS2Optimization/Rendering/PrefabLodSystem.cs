// Portions adapted from Cs2Saver (CS2PerformancePatcher, commit 69015dd90cfc667c100beb16fd158a43a9e03a23),
// Copyright (C) 2026 NSPX. Licensed under GPL-3.0; see LICENSE and THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using Colossal.Serialization.Entities;
using CS2Optimization.Core;
using Game;
using Game.Prefabs;
using Game.Rendering;
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
    /// <para><b>Prefabs, not instances.</b> The game re-seeds each instance's
    /// <c>CullingInfo.m_MinLod</c> from its prefab whenever the instance is updated, so writing
    /// the prefab is what makes the change stick. Instances that already exist are only re-seeded
    /// by the game on an update, though, so after every change this system re-seeds them once
    /// itself (see <see cref="ReseedInstances"/>). That is what makes Off take effect immediately
    /// instead of after a reload. It runs on changes only, never per frame.</para>
    ///
    /// <para><b>Originals.</b> Every prefab's own value is kept in a <see cref="LodLedger{TId}"/>
    /// for the current city, so passes never stack, and Off, city exit and mod unload all put the
    /// game's values back. <c>m_MinLod</c> is derived at prefab initialisation and not serialised,
    /// so nothing here can reach a save file.</para>
    /// </summary>
    public partial class PrefabLodSystem : GameSystemBase
    {
        private readonly LodLedger<Entity> m_Ledger = new LodLedger<Entity>();
        private readonly WarnOnce m_Warn = new WarnOnce();

        /// <summary>Prefabs that might be props. Final eligibility is decided per entity by the policy.</summary>
        private EntityQuery m_Candidates;

        /// <summary>Live objects whose culling floor was seeded from a prefab.</summary>
        private EntityQuery m_Instances;

        private ComponentTypeHandle<CullingInfo> m_CullingInfoType;
        private ComponentTypeHandle<PrefabRef> m_PrefabRefType;

        private int m_Offset;
        private int m_AppliedOffset;
        private bool m_Dirty = true;
        private int m_LastCandidateCount = -1;
        private bool m_Failed;

        /// <summary>Offset in effect on the prefabs this system tracks. 0 = game values.</summary>
        public int AppliedOffset => m_Failed ? 0 : m_AppliedOffset;

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
        public void RestoreNow() => Restore(reseedInstances: true);

        /// <summary>Restores and forgets the current city's originals; the next city starts fresh.</summary>
        public void ResetForNewCity()
        {
            // The city being left is about to be unloaded, so its instances need no re-seed.
            Restore(reseedInstances: false);
            m_Ledger.Clear();
            m_Dirty = true;
            m_LastCandidateCount = -1;
        }

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

            m_Instances = GetEntityQuery(
                ComponentType.ReadWrite<CullingInfo>(),
                ComponentType.ReadOnly<PrefabRef>());

            m_CullingInfoType = GetComponentTypeHandle<CullingInfo>(false);
            m_PrefabRefType = GetComponentTypeHandle<PrefabRef>(true);
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

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            Guard("city exit", ResetForNewCity);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_Dirty = true;
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
                Restore(reseedInstances: true);
                return;
            }

            var changes = new Dictionary<Entity, (int from, int to)>();
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

                    changes[entity] = (data.m_MinLod, write);
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
            if (changes.Count == 0) return;

            var reseeded = ReseedInstances(changes);
            Mod.Log.Info($"Prop LOD +{m_Offset}: {eligible} eligible prefabs, {changes.Count} updated, {reseeded} live objects re-seeded.");
        }

        private void Restore(bool reseedInstances)
        {
            if (m_Ledger.Count == 0)
            {
                m_AppliedOffset = 0;
                return;
            }

            var em = EntityManager;
            var changes = new Dictionary<Entity, (int from, int to)>();

            var result = m_Ledger.RestoreAll(
                entity => em.Exists(entity) && em.HasComponent<ObjectGeometryData>(entity)
                    ? em.GetComponentData<ObjectGeometryData>(entity).m_MinLod
                    : (int?)null,
                (entity, original) =>
                {
                    var data = em.GetComponentData<ObjectGeometryData>(entity);
                    changes[entity] = (data.m_MinLod, original);
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

            if (result.Restored == 0) return;

            var reseeded = reseedInstances ? ReseedInstances(changes) : 0;
            Mod.Log.Info($"Prop LOD restored: {result.Restored} prefabs, {reseeded} live objects re-seeded.");
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

        /// <summary>
        /// Does for existing objects what the game does when it re-seeds one: copies the prefab's
        /// floor into <c>CullingInfo.m_MinLod</c>. Only objects still holding the prefab's
        /// previous floor are changed, so any value the game derived differently is left alone.
        /// Main thread, once per change.
        /// </summary>
        private int ReseedInstances(Dictionary<Entity, (int from, int to)> changes)
        {
            if (changes.Count == 0 || m_Instances.IsEmptyIgnoreFilter) return 0;

            m_Instances.CompleteDependency();
            m_CullingInfoType.Update(this);
            m_PrefabRefType.Update(this);

            var reseeded = 0;
            var chunks = m_Instances.ToArchetypeChunkArray(Allocator.Temp);
            try
            {
                foreach (var chunk in chunks)
                {
                    var prefabs = chunk.GetNativeArray(ref m_PrefabRefType);
                    var infos = chunk.GetNativeArray(ref m_CullingInfoType);

                    for (var i = 0; i < prefabs.Length; i++)
                    {
                        if (!changes.TryGetValue(prefabs[i].m_Prefab, out var change)) continue;

                        var info = infos[i];
                        if (info.m_MinLod != change.from) continue;

                        info.m_MinLod = (byte)change.to;
                        infos[i] = info;
                        reseeded++;
                    }
                }
            }
            finally
            {
                chunks.Dispose();
            }

            return reseeded;
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
                    Restore(reseedInstances: false);
                }
                catch (Exception restoreEx)
                {
                    if (m_Warn.ShouldLog("restore-after-failure")) Mod.Log.Error(restoreEx, "Prop LOD restore after failure also failed.");
                }
            }
        }
    }
}
