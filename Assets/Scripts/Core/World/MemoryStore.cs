using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// MemoryStore: contenitore di tracce per un NPC.
    ///
    /// Giorno 2:
    /// - Conservare tracce
    /// - Fondere tracce "simili" (AddOrMerge)
    /// - Applicare decadimento per tick (TickDecay)
    /// 
    /// Giorno 3: aggiungiamo
    /// - Cap massimo tracce: evita crescita infinita
    /// - Pruning deterministico: se pieno, rimuove le tracce meno importanti
    /// - Helper per leggere le top-N tracce
    /// </summary>
    public sealed class MemoryStore
    {
        // Capacità massima per NPC: scelta conservativa.
        // Se in futuro vuoi più dettaglio, si alza.
        public int MaxTraces { get; set; } = 32;

        private readonly List<MemoryTrace> _traces = new(16);

        public IReadOnlyList<MemoryTrace> Traces => _traces;

        /// <summary>
        /// Aggiunge o fonde una traccia equivalente.
        ///
        /// Se lo store è pieno e la traccia è "debole",
        /// potrebbe essere scartata.
        /// </summary>
        public void AddOrMerge(in MemoryTrace incoming)
        {
            // 1) Prova merge con una traccia equivalente
            for (int i = 0; i < _traces.Count; i++)
            {
                var t = _traces[i];

                if (t.Type == incoming.Type &&
                    t.SubjectId == incoming.SubjectId &&
                    t.CellX == incoming.CellX &&
                    t.CellY == incoming.CellY)
                {
                    // Merge deterministico
                    float mergedIntensity = (t.Intensity01 > incoming.Intensity01) ? t.Intensity01 : incoming.Intensity01;

                    // Rinforzo: una "nuova occorrenza" rende la memoria più viva
                    mergedIntensity += 0.05f;
                    if (mergedIntensity > 1f) mergedIntensity = 1f;

                    t.Intensity01 = mergedIntensity;

                    // Affidabilità: prendi la migliore
                    t.Reliability01 = (t.Reliability01 > incoming.Reliability01) ? t.Reliability01 : incoming.Reliability01;

                    // Decay: scegli il più lento (min) => mantiene più a lungo
                    t.DecayPerTick01 = (t.DecayPerTick01 < incoming.DecayPerTick01) ? t.DecayPerTick01 : incoming.DecayPerTick01;

                    _traces[i] = t;
                    return;
                }
            }

            // 2) Se non c'è merge e lo store è pieno, decidiamo se scartare o sostituire.
            if (_traces.Count >= MaxTraces)
            {
                // Troviamo la "peggiore" traccia (meno importante) secondo una metrica semplice.
                // Metrica: importance = Intensity01 * Reliability01
                int worstIndex = -1;
                float worstImportance = float.MaxValue;

                for (int i = 0; i < _traces.Count; i++)
                {
                    var t = _traces[i];
                    float importance = t.Intensity01 * t.Reliability01;

                    if (importance < worstImportance)
                    {
                        worstImportance = importance;
                        worstIndex = i;
                    }
                }

                // Importanza della traccia entrante
                float incomingImportance = incoming.Intensity01 * incoming.Reliability01;

                // Se la nuova è peggiore o uguale alla peggiore esistente, la scartiamo.
                // Questo evita che spam di tracce deboli rimpiazzi tracce più significative.
                if (incomingImportance <= worstImportance)
                    return;

                // Altrimenti sostituiamo la peggiore
                _traces[worstIndex] = incoming;
                return;
            }

            // 3) Se c'è spazio, aggiungiamo
            _traces.Add(incoming);
        }

        /// <summary>
        /// Applica decadimento alle tracce.
        ///
        /// tickScale: tipicamente tick.DeltaTime (tempo simulato per tick)
        /// decayMultiplier: modulatore globale/individuale (Giorno 3: viene dai tratti NPC)
        /// </summary>
        public int TickDecay(float tickScale, float decayMultiplier)
        {
            if (_traces.Count == 0) return 0;

            int before = _traces.Count;

            // Compact in-place: manteniamo le tracce vive in testa.
            int write = 0;

            for (int read = 0; read < _traces.Count; read++)
            {
                var t = _traces[read];

                float decay = t.DecayPerTick01 * tickScale * decayMultiplier;
                t.Intensity01 -= decay;

                if (t.Intensity01 > 0f)
                {
                    _traces[write] = t;
                    write++;
                }
            }

            if (write < _traces.Count)
                _traces.RemoveRange(write, _traces.Count - write);

            return before - _traces.Count;
        }

        /// <summary>
        /// Ritorna fino a N tracce "più importanti".
        ///
        /// Importanza: Intensity01 * Reliability01
        /// - Non alloca liste nuove: scrive su output (riusabile)
        /// - Ordina con selezione semplice (N piccolo)
        /// </summary>
        public void GetTopTraces(int maxCount, List<MemoryTrace> output)
        {
            output.Clear();
            if (maxCount <= 0) return;
            if (_traces.Count == 0) return;

            // Copia tutto in output (poi potiamo)
            for (int i = 0; i < _traces.Count; i++)
                output.Add(_traces[i]);

            // Selection sort parziale: porta davanti le più importanti.
            int limit = (maxCount < output.Count) ? maxCount : output.Count;

            for (int i = 0; i < limit; i++)
            {
                int bestIndex = i;
                float bestScore = Score(output[i]);

                for (int j = i + 1; j < output.Count; j++)
                {
                    float s = Score(output[j]);
                    if (s > bestScore)
                    {
                        bestScore = s;
                        bestIndex = j;
                    }
                }

                if (bestIndex != i)
                {
                    var tmp = output[i];
                    output[i] = output[bestIndex];
                    output[bestIndex] = tmp;
                }
            }

            // Rimuovi oltre limit
            if (output.Count > limit)
                output.RemoveRange(limit, output.Count - limit);
        }

        private static float Score(in MemoryTrace t)
        {
            return t.Intensity01 * t.Reliability01;
        }
    }
}
