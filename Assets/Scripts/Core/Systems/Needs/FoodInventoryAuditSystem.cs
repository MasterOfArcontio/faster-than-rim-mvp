using Arcontio.Core.Diagnostics;
using Arcontio.Core.Logging;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// FoodInventoryAuditSystem (Day9 - rumor/sospetto):
    /// Confronta private food tra tick precedente e tick corrente.
    ///
    /// Se il valore diminuisce, emette FoodMissingSuspectedEvent.
    ///
    /// IMPORTANTI FIX:
    /// - Non usare "return" dentro il loop (altrimenti interrompi il controllo sugli altri NPC).
    /// - Segno: se prev > cur => missing = prev - cur.
    /// </summary>
    public sealed class FoodInventoryAuditSystem : ISystem
    {
        public int Period => 1;

        private readonly Dictionary<int, int> _lastPrivateFood = new();
        private readonly List<int> _npcIds = new(2048);

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            int suspected = 0;

            for (int i = 0; i < _npcIds.Count; i++)
            {
                int npcId = _npcIds[i];

                world.NpcPrivateFood.TryGetValue(npcId, out int cur);

                // Primo tick per questo NPC: init e stop (ma NON return globale)
                if (!_lastPrivateFood.TryGetValue(npcId, out int prev))
                {
                    _lastPrivateFood[npcId] = cur;
                    continue;
                }

                // Se è diminuito, manca cibo
                if (cur < prev)
                {
                    int missingUnits = prev - cur;

                    int cx = 0, cy = 0;
                    if (world.GridPos.TryGetValue(npcId, out var p))
                    {
                        cx = p.X;
                        cy = p.Y;
                    }

                    bus.Publish(new FoodMissingSuspectedEvent(
                        victimNpcId: npcId,
                        missingUnits: missingUnits,
                        cellX: cx,
                        cellY: cy
                    ));

                    suspected++;

                    ArcontioLogger.Info(
                        new LogContext(tick: (int)tick.Index, channel: "T9", npcId: npcId),
                        new LogBlock(LogLevel.Info, "log.t9.suspect.missing_private_food")
                            .AddField("missingUnits", missingUnits)
                            .AddField("prev", prev)
                            .AddField("cur", cur)
                    );
                }

                // Update snapshot sempre
                _lastPrivateFood[npcId] = cur;
            }

            telemetry.Counter("Day9.FoodMissingSuspectedEvents", suspected);
        }
    }
}
