using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartPageDuplicate.Copy
{
    /// <summary>
    /// A fordítás során összegyűlt megfigyelések. A fordító nem ír a felületre és nem dönt a
    /// felhasználó helyett - csak jelenti, mi történt; a megjelenítés és a döntés a hívóé.
    /// </summary>
    public class TranslationReport
    {
        private readonly List<string> _skipped = new();
        private readonly List<string> _blocking = new();
        private readonly Dictionary<string, int> _conversions = new();

        /// <summary>Ami kimaradt a másolatból, de a művelet folytatható.</summary>
        public IReadOnlyList<string> Skipped => _skipped;

        /// <summary>Amivel a mentés biztosan elbukna - ilyenkor el sem érdemes indítani.</summary>
        public IReadOnlyList<string> Blocking => _blocking.Distinct().ToList();

        /// <summary>Típusonként hány hivatkozás fordult át ténylegesen más azonosítóra.</summary>
        public IReadOnlyDictionary<string, int> Conversions => _conversions;

        public bool HasBlockingProblems => _blocking.Count > 0;

        public void Skip(string message) => _skipped.Add(message);

        public void Block(string message) => _blocking.Add(message);

        public void Converted(string what)
        {
            _conversions.TryGetValue(what, out int count);
            _conversions[what] = count + 1;
        }

        public void Clear()
        {
            _skipped.Clear();
            _blocking.Clear();
            _conversions.Clear();
        }
    }
}
