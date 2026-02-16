using Arcontio.Core.Diagnostics;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// ObjectMemoryMaintenanceSystem:
    /// pulizia periodica della memoria oggetti interagibili per NPC.
    ///
    /// Politica v0:
    /// - non rimuove mai IsPinned (oggetti posseduti dall'NPC)
    /// - rimuove entries non-pinned troppo vecchie (maxAgeTicks)
    /// </summary>
    public sealed class ObjectMemoryMaintenanceSystem : ISystem
    {
        public int Period => 50; // esempio: ogni 50 tick (aggiusta a piacere)

        private readonly List<int> _npcIds = new(2048);

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (world.NpcCore.Count == 0) return;

            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            int cleaned = 0;
            int now = (int)tick.Index;

            int maxAge = world.Global.ObjectMemoryMaxAgeTicks;
            if (maxAge <= 0) maxAge = 500;

            for (int i = 0; i < _npcIds.Count; i++)
            {
                int npcId = _npcIds[i];
                if (!world.NpcObjectMemory.TryGetValue(npcId, out var store) || store == null)
                    continue;

                store.Cleanup(now, maxAge);
                cleaned++;
            }

            telemetry.Counter("ObjectMemory.CleanedNpcStores", cleaned);
        }
    }
}
