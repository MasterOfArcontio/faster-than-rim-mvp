using Arcontio.Core.Diagnostics;
using Arcontio.Core.Logging;
using System.Collections.Generic;
using UnityEngine;


namespace Arcontio.Core
{
    /// <summary>
    /// TheftSuspicionSystem (Day9):
    /// Crea un sospetto quando un NPC nota che il suo cibo privato è diminuito.
    ///
    /// Filosofia:
    /// - Il furto è "verità del mondo" (FoodStolenEvent).
    /// - La vittima NON lo sa automaticamente.
    /// - Se la vittima non ha percepito il furto, può comunque dedurre che manca cibo:
    ///   -> evento di "sospetto", reliability bassa.
    ///
    /// Limiti v0:
    /// - Non distingue cause alternative (consumo, scambio, ecc.).
    /// - È ok per il test Day9: serve solo a generare una traccia "FoodMissingSuspected".
    /// </summary>
    public sealed class TheftSuspicionSystem : ISystem
    {
        public int Period => 1;

        // Snapshot "tick precedente": npcId -> privateFoodUnits
        private readonly Dictionary<int, int> _prevPrivateFood = new();

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (world.NpcCore.Count == 0)
                return;

            int suspected = 0;

            // Per ogni NPC, confronta prev vs current.
            foreach (var npcId in world.NpcCore.Keys)
            {
                world.NpcPrivateFood.TryGetValue(npcId, out int cur);

                if (_prevPrivateFood.TryGetValue(npcId, out int prev))
                {
                    // Se è diminuito, l’NPC può notarlo.
                    // (In un gioco vero userai: accesso all’inventario, contatore, routine giornaliere, ecc.)
                    if (cur < prev)
                    {
                        int missing = prev - cur;

                        // Se l’NPC ha consumato privato in questo tick, NON è "sospetto"
                        if (world.NpcLastPrivateFoodConsumeTick.TryGetValue(npcId, out long lastEatTick) &&
                            lastEatTick == tick.Index)
                        {
                            // Aggiorno snapshot e vado avanti senza pubblicare sospetto
                            _prevPrivateFood[npcId] = cur;
                            continue;
                        }

                        int ex = 0, ey = 0;
                        if (world.GridPos.TryGetValue(npcId, out var p))
                        {
                            ex = p.X;
                            ey = p.Y;
                        }

                        bus.Publish(new FoodMissingSuspectedEvent(
                            victimNpcId: npcId,
                            missingUnits: missing,
                            cellX: ex,
                            cellY: ey
                        ));

                        suspected++;
                        ArcontioLogger.Info(
                            new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T9", npcId: npcId),
                            new LogBlock(LogLevel.Info, "log.t9.suspect.missing_food")
                                .AddField("missing", missing)
                                .AddField("prev", prev)
                                .AddField("cur", cur)
                        );
                    }
                }

                // Aggiorna snapshot
                _prevPrivateFood[npcId] = cur;
            }

            telemetry.Counter("Day9.FoodMissingSuspectedEvents", suspected);
        }
    }
}
