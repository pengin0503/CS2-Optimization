using System.Collections.Generic;

namespace CS2Optimization.Core
{
    /// <summary>Keeps a recurring problem to one log line instead of one per frame.</summary>
    public sealed class WarnOnce
    {
        private readonly HashSet<string> m_Seen = new HashSet<string>();

        public bool ShouldLog(string key) => m_Seen.Add(key);

        public void Reset() => m_Seen.Clear();
    }
}
