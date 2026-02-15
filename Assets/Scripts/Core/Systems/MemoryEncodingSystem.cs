using Arcontio.Core.Diagnostics;
using System;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// MemoryEncodingSystem:
    /// - prende gli eventi del tick (buffer)
    /// - decide per quali NPC l'evento è percepito (testimoni)  <-- QUI applichiamo CONO+LOS
    /// - per ciascun testimone applica IMemoryRule e aggiunge trace nel MemoryStore
    ///
    /// Nota architetturale:
    /// - Questo system NON legge il MessageBus direttamente (coda).
    /// - Gli eventi vengono "drainati" in StepOneTick e passati qui tramite SetEventsBuffer.
    /// </summary>
    public sealed class MemoryEncodingSystem : ISystem
    {
        public int Period => 1;

        private readonly List<IMemoryRule> _rules = new();

        // Buffer di eventi del tick (riusato, assegnato dal SimulationHost)
        private List<ISimEvent> _eventsBuffer;

        // Buffer ids NPC per iterazione
        private readonly List<int> _npcIds = new(2048);

        public MemoryEncodingSystem()
        {
            // Catalogo minimo rules (espandibile)
            _rules.Add(new PredatorSpottedMemoryRule());
            _rules.Add(new AttackWitnessedMemoryRule());
            _rules.Add(new DeathWitnessedMemoryRule());

            // Oggetti visti -> memoria
            _rules.Add(new ObjectSpottedMemoryRule());

            // Furto cibo (Day9)
            _rules.Add(new FoodStolenMemoryRule());

            // Se hai una rule per FoodMissingSuspectedEvent, aggiungila qui.
            // _rules.Add(new FoodMissingSuspectedMemoryRule());
        }

        /// <summary>
        /// Il SimulationHost assegna qui la lista di eventi drainata dal bus.
        /// </summary>
        public void SetEventsBuffer(List<ISimEvent> eventsBuffer)
        {
            _eventsBuffer = eventsBuffer;
        }

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (_eventsBuffer == null || _eventsBuffer.Count == 0)
                return;

            // Snapshot parametri percezione dal GlobalState:
            int visionRange = world.Global.NpcVisionRangeCells;
            if (visionRange <= 0) visionRange = 6;

            // Cono:
            // Nel tuo progetto hai sia NpcVisionUseCone/NpcVisionConeSlope
            // sia NpcVisionConeHalfWidthPerStep. Per evitare ambiguità,
            // usiamo: (A) UseCone toggle + (B) ConeSlope come ampiezza.
            bool useCone = world.Global.NpcVisionUseCone;
            float coneHalfWidthPerStep = world.Global.NpcVisionConeSlope;
            if (coneHalfWidthPerStep < 0f) coneHalfWidthPerStep = 0f;

            // LOS:
            // Riutilizzo il toggle già esistente (EnableTokenLOS) per non introdurre un nuovo flag.
            // Se vuoi separare le due cose: aggiungi GlobalState.NpcVisionUseLOS.
            bool useLos = world.Global.EnableTokenLOS;

            // Preleva lista NPC
            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            int tracesAdded = 0;

            // Per ogni evento, proviamo a creare memorie per i testimoni.
            for (int eIdx = 0; eIdx < _eventsBuffer.Count; eIdx++)
            {
                var e = _eventsBuffer[eIdx];

                for (int r = 0; r < _rules.Count; r++)
                {
                    var rule = _rules[r];
                    if (!rule.Matches(e))
                        continue;

                    // ? FIX CS0136: evX/evY invece di ex/ey
                    if (!TryGetEventCell(e, out int evX, out int evY))
                        break; // evento senza cella -> non possiamo decidere testimoni in v0

                    for (int n = 0; n < _npcIds.Count; n++)
                    {
                        int npcId = _npcIds[n];

                        if (!world.GridPos.TryGetValue(npcId, out var p))
                            continue;

                        int dist = Manhattan(p.X, p.Y, evX, evY);
                        if (dist > visionRange)
                            continue;

                        // ? CONO (orientamento) per tutti gli eventi con cella
                        if (useCone)
                        {
                            if (!world.NpcFacing.TryGetValue(npcId, out var facing))
                                facing = CardinalDirection.North;

                            if (!IsInCone(p.X, p.Y, facing, evX, evY, coneHalfWidthPerStep))
                                continue;
                        }

                        // ? LOS per tutti gli eventi con cella
                        // Nota: se vuoi un toggle globale, puoi usare world.Global.EnableTokenLOS oppure creare EnableNpcVisionLOS.
                        // Qui assumo: BlocksVision + VisionCost>=1 => blocca.
                        if (HasBlockingLOS(world, p.X, p.Y, evX, evY))
                            continue;

                        float quality = 1f - (dist / (float)visionRange);
                        if (quality < 0.05f) quality = 0.05f;

                        telemetry.Counter("MemoryEncodingSystem.TracesEncodedAttempts", 1);

                        if (rule.TryEncode(world, npcId, e, quality, out var trace))
                        {
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

                    break; // una rule per evento
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

        /// <summary>
        /// IsInCone:
        /// Cono in griglia deterministico, basato su:
        /// - forward: quanto è davanti (deve essere > 0)
        /// - side: quanto è laterale (|side| <= forward * coneHalfWidthPerStep)
        ///
        /// coneHalfWidthPerStep:
        /// - 0.0  => solo linea frontale
        /// - 0.5  => cono stretto
        /// - 1.0  => cono ampio (?45° su griglia)
        /// </summary>
        private static bool IsInCone(int sx, int sy, CardinalDirection facing, int tx, int ty, float coneHalfWidthPerStep)
        {
            int dx = tx - sx;
            int dy = ty - sy;

            int forward, side;

            switch (facing)
            {
                case CardinalDirection.North:
                    forward = dy;
                    side = dx;
                    break;

                case CardinalDirection.South:
                    forward = -dy;
                    side = -dx;
                    break;

                case CardinalDirection.East:
                    forward = dx;
                    side = -dy;
                    break;

                case CardinalDirection.West:
                    forward = -dx;
                    side = dy;
                    break;

                default:
                    return false;
            }

            if (forward <= 0)
                return false;

            int absSide = side < 0 ? -side : side;

            // absSide <= forward * slope
            // Usiamo floor per mantenere determinismo su grid.
            int allowed = (int)Math.Floor(forward * coneHalfWidthPerStep + 0.0001f);
            return absSide <= allowed;
        }

        // =========================
        // LOS (blocca)
        // =========================
        private static bool HasBlockingLOS(World world, int x0, int y0, int x1, int y1)
        {
            foreach (var cell in BresenhamCellsBetween(x0, y0, x1, y1))
            {
                if (!world.TryGetOccluder(cell.x, cell.y, out var occ))
                    continue;

                if (!occ.BlocksVision)
                    continue;

                // muro pieno -> blocca
                if (occ.VisionCost >= 1f)
                    return true;
            }
            return false;
        }

        private static IEnumerable<(int x, int y)> BresenhamCellsBetween(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);

            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;

            int err = dx - dy;
            int x = x0;
            int y = y0;

            while (!(x == x1 && y == y1))
            {
                int e2 = 2 * err;

                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }

                // escludiamo start e end
                if (x == x1 && y == y1) yield break;
                if (x == x0 && y == y0) continue;

                yield return (x, y);
            }
        }

        private static bool TryGetEventCell(ISimEvent e, out int x, out int y)
        {
            // Pattern match: qui teniamo la "mappa" di cosa ha una cella.
            switch (e)
            {
                case AttackEvent a:
                    x = a.CellX; y = a.CellY; return true;

                case DeathEvent d:
                    x = d.CellX; y = d.CellY; return true;

                case PredatorSpottedEvent p:
                    x = p.CellX; y = p.CellY; return true;

                case ObjectSpottedEvent o:
                    x = o.CellX; y = o.CellY; return true;

                // Day9: furto cibo (FACT con cella)
                case FoodStolenEvent fs:
                    x = fs.CellX; y = fs.CellY; return true;

                // Day9: sospetto di mancanza (evento “interno”, ma con cella dove “se ne accorge”)
                case FoodMissingSuspectedEvent ms:
                    x = ms.CellX; y = ms.CellY; return true;

                default:
                    x = y = 0;
                    return false;
            }
        }
    }
}
