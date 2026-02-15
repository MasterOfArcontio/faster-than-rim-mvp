using System.Collections.Generic;
using Arcontio.Core.Diagnostics;

namespace Arcontio.Core
{
    /// <summary>
    /// PrivateFoodAuditSystem (Day9):
    /// Deduzione v0: un NPC nota che il suo cibo privato è diminuito rispetto all'ultima verifica.
    ///
    /// - NON dice chi è stato.
    /// - Produce FoodMissingEvent -> FoodMissingMemoryRule -> memoria sospetto.
    ///
    /// Nota importante:
    /// - Questo system è una scorciatoia "di gameplay" per testare il ramo rumor/sospetto.
    /// - In futuro l'audit sarà un'azione (l'NPC controlla la scorta).
    /// </summary>
    public sealed class PrivateFoodAuditSystem : ISystem
    {
        public int Period => 1;

        private readonly List<int> _npcIds = new(2048);

        // Ledger interno (runtime): ultimo valore noto per ogni NPC
        private readonly Dictionary<int, int> _lastAuditFood = new();

        private readonly int _auditEveryTicks;

        public PrivateFoodAuditSystem(int auditEveryTicks = 20)
        {
            _auditEveryTicks = auditEveryTicks <= 0 ? 20 : auditEveryTicks;
        }

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (world.NpcCore.Count == 0) return;

            if ((tick.Index % _auditEveryTicks) != 0)
                return;

            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            int missingEvents = 0;

            for (int i = 0; i < _npcIds.Count; i++)
            {
                int npcId = _npcIds[i];

                // Se l'NPC non ha entry, assumiamo 0
                int current = 0;
                world.NpcPrivateFood.TryGetValue(npcId, out current);

                if (!_lastAuditFood.TryGetValue(npcId, out int last))
                {
                    // primo audit: inizializza e basta
                    _lastAuditFood[npcId] = current;
                    continue;
                }

                if (current < last)
                {
                    int missing = last - current;

                    bus.Publish(new FoodMissingEvent(victimNpcId: npcId, missingUnits: missing));
                    missingEvents++;

                    telemetry.Counter("Day9.FoodMissingEvents", 1);
                }

                _lastAuditFood[npcId] = current;
            }

            telemetry.Counter("Day9.AuditRuns", 1);
            telemetry.Counter("Day9.AuditMissingEventsBatch", missingEvents);
        }
    }
}
