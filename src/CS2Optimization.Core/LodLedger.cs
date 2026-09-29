using System.Collections.Generic;

namespace CS2Optimization.Core
{
    /// <summary>
    /// Remembers each prefab's LOD floor as the game derived it, so every pass is computed from
    /// that original rather than from the last result. That makes re-applying idempotent,
    /// changing the offset exact, and turning the mod off a true restore.
    ///
    /// <para>One ledger belongs to one city. Entity ids can be reused once a city is unloaded,
    /// so the owner restores and then <see cref="Clear"/>s it before the next city.</para>
    ///
    /// <para>Records only; enumerating and writing the prefabs is the caller's job.</para>
    /// </summary>
    public sealed class LodLedger<TId> where TId : notnull
    {
        public const int MaxLod = 255;

        private struct Entry
        {
            public int Original;

            /// <summary>The value this ledger last wrote (or left in place).</summary>
            public int Applied;
        }

        private readonly Dictionary<TId, Entry> m_Entries = new Dictionary<TId, Entry>();

        public int Count => m_Entries.Count;

        /// <summary>Every tracked id with its original value, for restoring.</summary>
        public IEnumerable<KeyValuePair<TId, int>> Entries
        {
            get
            {
                foreach (var pair in m_Entries)
                    yield return new KeyValuePair<TId, int>(pair.Key, pair.Value.Original);
            }
        }

        /// <summary>The value the floor should have for <paramref name="offset"/>, within 0..255.</summary>
        public static int Target(int original, int offset)
        {
            var target = original + offset;
            if (target < 0) return 0;
            return target > MaxLod ? MaxLod : target;
        }

        /// <summary>
        /// The original floor for <paramref name="id"/>. Captured from <paramref name="current"/>
        /// on first sight, and re-captured if <paramref name="current"/> is no longer the value
        /// this ledger left there: then the game has re-derived it and that is the new truth.
        /// </summary>
        public int GetOrCapture(TId id, int current)
        {
            if (m_Entries.TryGetValue(id, out var entry) && entry.Applied == current) return entry.Original;

            m_Entries[id] = new Entry { Original = current, Applied = current };
            return current;
        }

        /// <summary>
        /// Decides the value for <paramref name="id"/> at <paramref name="offset"/> and records
        /// it as applied. Returns false when <paramref name="current"/> already matches, so the
        /// caller can skip the write.
        /// </summary>
        public bool Plan(TId id, int current, int offset, out int write)
        {
            var original = GetOrCapture(id, current);
            write = Target(original, offset);
            m_Entries[id] = new Entry { Original = original, Applied = write };
            return write != current;
        }

        /// <summary>
        /// The original value to write back, but only while the value this ledger applied is
        /// still in place. Anything else means someone changed it since, and writing the old
        /// original over their value would be wrong. On success the entry stays, now marked as
        /// holding the original.
        /// </summary>
        public bool TryRestore(TId id, int current, out int original)
        {
            original = 0;
            if (!m_Entries.TryGetValue(id, out var entry) || entry.Applied != current) return false;

            original = entry.Original;
            m_Entries[id] = new Entry { Original = original, Applied = original };
            return true;
        }

        /// <summary>Outcome of <see cref="RestoreAll"/>.</summary>
        public readonly struct RestoreResult
        {
            public RestoreResult(int restored, int missing, int changedElsewhere)
            {
                Restored = restored;
                Missing = missing;
                ChangedElsewhere = changedElsewhere;
            }

            /// <summary>Originals written back.</summary>
            public int Restored { get; }

            /// <summary>Ids that no longer exist; dropped without writing.</summary>
            public int Missing { get; }

            /// <summary>Values someone else changed since; left as they are.</summary>
            public int ChangedElsewhere { get; }
        }

        /// <summary>
        /// Writes every original back that can safely be written. <paramref name="read"/> returns
        /// null when the id no longer exists; such entries are dropped without writing. Entries
        /// whose value was changed by someone else since are left untouched.
        /// </summary>
        public RestoreResult RestoreAll(System.Func<TId, int?> read, System.Action<TId, int> write)
        {
            var restored = 0;
            var missing = 0;
            var changed = 0;
            var ids = new List<TId>(m_Entries.Keys);

            foreach (var id in ids)
            {
                var current = read(id);
                if (current == null)
                {
                    m_Entries.Remove(id);
                    missing++;
                    continue;
                }

                if (!TryRestore(id, current.Value, out var original))
                {
                    changed++;
                    continue;
                }

                if (original == current.Value) continue;

                write(id, original);
                restored++;
            }

            return new RestoreResult(restored, missing, changed);
        }

        /// <summary>Stops tracking an id, e.g. when its entity no longer exists.</summary>
        public void Forget(TId id) => m_Entries.Remove(id);

        /// <summary>Drops every record. Call after restoring, when a city is left.</summary>
        public void Clear() => m_Entries.Clear();
    }
}
