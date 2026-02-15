using System.Collections.Generic;
using Arcontio.Core.Diagnostics;

namespace Arcontio.Core
{
    /// <summary>
    /// NeedsDecaySystem (Day9):
    /// System deterministico “basso livello”.
    ///
    /// - Applica fame/stanchezza ad ogni tick usando NeedsConfig.
    /// - NON decide cosa fare (quello sta nelle Rules).
    /// </summary>
    public sealed class NeedsDecaySystem : ISystem
    {
        public int Period => 1;

        private readonly List<int> _npcIds = new(2048);

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (world.NpcCore.Count == 0) return;

            var cfg = world.Global.Needs;

            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            int updated = 0;

            for (int i = 0; i < _npcIds.Count; i++)
            {
                int npcId = _npcIds[i];
                if (!world.Needs.TryGetValue(npcId, out var n)) continue;

                // Hunger01 cresce con il tempo (più fame)
                n.Hunger01 += cfg.satietyDecayPerTick;
                if (n.Hunger01 > 1f) n.Hunger01 = 1f;

                // Fatigue01 cresce con il tempo (più stanchezza)
                n.Fatigue01 += cfg.restDecayPerTick;
                if (n.Fatigue01 > 1f) n.Fatigue01 = 1f;

                // Flag comodo (opzionale)
                n.IsHungry = n.Hunger01 >= cfg.hungryThreshold;

                world.Needs[npcId] = n;
                updated++;
            }

            telemetry.Counter("NeedsDecay.Updated", updated);
        }
    }
}
