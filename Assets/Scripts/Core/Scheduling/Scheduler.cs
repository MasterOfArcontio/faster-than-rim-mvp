using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// Scheduler: decide quali systems eseguire ad ogni tick.
    ///
    /// Obiettivo:
    /// - supportare tanti systems senza iterarli tutti ogni tick
    /// - ogni system dichiara un Period (es. 1, 5, 60)
    /// - lo scheduler raggruppa i systems per Period
    ///
    /// FIX (importante):
    /// - Rendiamo l'ordine di esecuzione deterministico.
    ///   Perché? Perché alcuni sistemi devono girare prima di altri (es. encoding memoria prima del decay).
    ///
    /// Come lo rendiamo deterministico?
    /// - Ordiniamo i "periodi" (1, 5, 60, ...) in modo stabile (crescente).
    /// - Dentro ogni periodo manteniamo l'ordine di inserimento (AddSystem).
    /// </summary>
    public sealed class Scheduler
    {
        private readonly List<ISystem> _all = new();

        // Raggruppa i sistemi per periodo.
        // NOTA: un Dictionary NON garantisce un ordine "significativo" quando lo scorri.
        private readonly Dictionary<int, List<ISystem>> _periodBuckets = new();

        // Buffer riusabile per non allocare ogni tick quando ordiniamo le chiavi.
        private readonly List<int> _sortedPeriods = new();

        public void AddSystem(ISystem sys)
        {
            _all.Add(sys);

            if (!_periodBuckets.TryGetValue(sys.Period, out var list))
            {
                list = new List<ISystem>();
                _periodBuckets[sys.Period] = list;
            }

            // Dentro lo stesso periodo, l'ordine qui è quello con cui aggiungi i systems.
            list.Add(sys);
        }

        /// <summary>
        /// Restituisce solo i systems che devono girare in questo tick, in ordine deterministico.
        /// </summary>
        public void GetSystemsToRun(long tickIndex, List<ISystem> output)
        {
            output.Clear();

            // 1) Prendiamo tutti i periodi presenti
            _sortedPeriods.Clear();
            foreach (var p in _periodBuckets.Keys)
                _sortedPeriods.Add(p);

            // 2) Li ordiniamo (1, 5, 10, 60, ...)
            _sortedPeriods.Sort();

            // 3) Per ogni periodo, se il tick è multiplo del periodo, aggiungiamo quei systems
            for (int i = 0; i < _sortedPeriods.Count; i++)
            {
                int period = _sortedPeriods[i];
                if (period <= 0) continue;

                // "tickIndex % period == 0" significa: tickIndex è multiplo di period
                // Esempio: period=5 => girerà ai tick 0,5,10,15,...
                if (tickIndex % period == 0)
                {
                    // Dentro lo stesso periodo, l'ordine dei systems è quello di AddSystem
                    output.AddRange(_periodBuckets[period]);
                }
            }
        }
    }
}
