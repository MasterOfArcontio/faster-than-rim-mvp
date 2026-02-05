using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// Telemetry: raccolta contatori e info di debug senza inquinare i systems.
    /// Puoi sostituirla con un logger più serio in futuro.
    /// </summary>
    public sealed class Telemetry
    {
        private readonly Dictionary<string, long> _counters = new();

        public void Counter(string name, long delta)
        {
            if (_counters.TryGetValue(name, out var v)) _counters[name] = v + delta;
            else _counters[name] = delta;
        }

        public IReadOnlyDictionary<string, long> Counters => _counters;

        public void DumpToConsole()
        {
            foreach (var kv in _counters)
                Debug.Log($"[Telemetry] {kv.Key} = {kv.Value}");
        }
    }
}
