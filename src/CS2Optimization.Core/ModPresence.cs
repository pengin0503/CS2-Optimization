using System.Collections.Generic;

namespace CS2Optimization.Core
{
    /// <summary>Other mods that change what this one does, detected by loaded assembly name.</summary>
    public readonly struct ModPresence
    {
        public const string WriteEverywhereAssembly = "BelzontWE";
        public const string Cs2SaverAssembly = "Cs2Saver";

        public ModPresence(bool writeEverywhere, bool cs2Saver)
        {
            WriteEverywhere = writeEverywhere;
            Cs2Saver = cs2Saver;
        }

        public bool WriteEverywhere { get; }

        public bool Cs2Saver { get; }

        /// <summary>Exact (case-insensitive) assembly-name match; lookalike names do not count.</summary>
        public static ModPresence Detect(IEnumerable<string?> assemblyNames)
        {
            var we = false;
            var saver = false;
            foreach (var name in assemblyNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                we |= string.Equals(name, WriteEverywhereAssembly, System.StringComparison.OrdinalIgnoreCase);
                saver |= string.Equals(name, Cs2SaverAssembly, System.StringComparison.OrdinalIgnoreCase);
            }

            return new ModPresence(we, saver);
        }
    }
}
