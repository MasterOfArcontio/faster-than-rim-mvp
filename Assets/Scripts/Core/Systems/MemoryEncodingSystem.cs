using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// MemoryEncodingSystem:
    /// - prende gli eventi del tick (buffer)
    /// - decide per quali NPC l'evento è percepito (testimoni)
    /// - per ciascun testimone applica IMemoryRule e aggiunge trace nel MemoryStore
    ///
    /// Nota architetturale:
    /// - Questo system NON legge il MessageBus direttamente (coda).
    /// - Gli eventi vengono "drainati" in StepOneTick e passati qui tramite Init/SetBuffer.
    /// </summary>
    public sealed class MemoryEncodingSystem : ISystem
    {
        public int Period => 1;

        private readonly List<IMemoryRule> _rules = new();

        // Buffer di eventi del tick (riusato, assegnato dal SimulationHost)
        //private List<IWorldEvent> _eventsBuffer;
        private List<ISimEvent> _eventsBuffer;

        // Buffer ids NPC per iterazione
        private readonly List<int> _npcIds = new(2048);

        public MemoryEncodingSystem()
        {
            // Catalogo minimo rules (espandibile)
            _rules.Add(new PredatorSpottedMemoryRule());
            _rules.Add(new AttackWitnessedMemoryRule());
            _rules.Add(new DeathWitnessedMemoryRule());

            // NEW (Giorno 8): oggetti visti -> memoria
            _rules.Add(new ObjectSpottedMemoryRule());
        }

        /// <summary>
        /// Il SimulationHost assegna qui la lista di eventi drainata dal bus.
        /// </summary>
        /*public void SetEventsBuffer(List<IWorldEvent> eventsBuffer)
        {
            _eventsBuffer = eventsBuffer;
        }*/
        public void SetEventsBuffer(List<ISimEvent> eventsBuffer)
        {
            _eventsBuffer = eventsBuffer;
        }

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (_eventsBuffer == null || _eventsBuffer.Count == 0)
                return;

            // Preleva lista NPC (per ora: tutti gli NPC).
            // In futuro ottimizzeremo con spatial index.
            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            int tracesAdded = 0;

            // Per ogni evento, proviamo a creare memorie per i testimoni.
            for (int eIdx = 0; eIdx < _eventsBuffer.Count; eIdx++)
            {
                var e = _eventsBuffer[eIdx];

                // Trova una rule compatibile (oggi: poche, quindi lineare è ok)
                for (int r = 0; r < _rules.Count; r++)
                {
                    var rule = _rules[r];
                    if (!rule.Matches(e))
                        continue;

                    // Per ogni NPC valutiamo se può essere testimone.
                    for (int n = 0; n < _npcIds.Count; n++)
                    {
                        int npcId = _npcIds[n];

                        // Perception quality: per ora basata solo su distanza e range.
                        // - se l'evento ha cella, usiamo quella
                        // - se non ha cella, skip
                        if (!TryGetEventCell(e, out int ex, out int ey))
                            continue;

                        if (!world.GridPos.TryGetValue(npcId, out var p))
                            continue;

                        // Range visivo: per ora usiamo un default fisso.
                        // (Poi lo sposteremo su PerceptionComponent)
                        int visionRange = 6;

                        int dist = Manhattan(p.X, p.Y, ex, ey);
                        if (dist > visionRange)
                            continue;

                        // Qualità testimonianza: più lontano => peggiore
                        float quality = 1f - (dist / (float)visionRange);
                        if (quality < 0.05f) quality = 0.05f;

                        telemetry.Counter("MemoryEncodingSystem.TracesEncodedAttempts", 1);

                        if (rule.TryEncode(world, npcId, e, quality, out var trace))
                        {
                            // Inserisci nello store dell'NPC
                            var res = world.Memory[npcId].AddOrMerge(trace);

                            switch (res)
                            {
                                case AddOrMergeResult.Inserted:
                                    telemetry.Counter("MemoryEncodingSystem.TracesActuallyInserted", 1);
                                    break;
                                case AddOrMergeResult.Replaced:
                                    telemetry.Counter("MemoryEncodingSystem.TracesActuallyInserted", 1);
                                    telemetry.Counter("MemoryEncodingSystem.TracesReplaced", 1);
                                    break;
                                case AddOrMergeResult.Reinforced:
                                    telemetry.Counter("MemoryEncodingSystem.TracesReinforced", 1);
                                    break;
                                case AddOrMergeResult.Dropped:
                                    telemetry.Counter("MemoryEncodingSystem.TracesDropped", 1);
                                    break;
                            }

                            tracesAdded++;
                        }
                    }

                    // Regola trovata e applicata: non cerchiamo altre rule per lo stesso evento.
                    break;
                }
            }

            telemetry.Counter("MemoryEncodingSystem.TracesAdded", tracesAdded);
        }

        private static int Manhattan(int ax, int ay, int bx, int by)
        {
            int dx = ax - bx; if (dx < 0) dx = -dx;
            int dy = ay - by; if (dy < 0) dy = -dy;
            return dx + dy;
        }

//        private static bool TryGetEventCell(IWorldEvent e, out int x, out int y)
        private static bool TryGetEventCell(ISimEvent e, out int x, out int y)
        {
            // In questa fase, estraiamo la cella con pattern match.
            // In futuro potresti introdurre un'interfaccia IHasCell.
            switch (e)
            {
                case AttackEvent a:
                    x = a.CellX; y = a.CellY; return true;
                case DeathEvent d:
                    x = d.CellX; y = d.CellY; return true;
                case PredatorSpottedEvent p:
                    x = p.CellX; y = p.CellY; return true;

                // NEW (Giorno 8)
                case ObjectSpottedEvent o:
                    x = o.CellX; y = o.CellY; return true;

                default:
                    x = y = 0;
                    return false;
            }
        }
    }
}
