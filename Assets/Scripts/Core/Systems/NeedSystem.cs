using System;
using System.Collections.Generic;

namespace Arcontio.Core
{
    public sealed class NeedsSystem : ISystem
    {
        public int Period => 1;

        private readonly List<int> _ids = new(2048);

        private const float HungryThreshold = 0.80f;

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            _ids.Clear();
            _ids.AddRange(world.Needs.Keys);

            int updated = 0;

            for (int i = 0; i < _ids.Count; i++)
            {
                int id = _ids[i];

                if (!world.Needs.TryGetValue(id, out var needs))
                    continue;

                // update valori continui
                needs.Hunger01 = MathF.Min(1f, needs.Hunger01 + needs.HungerRate * tick.DeltaTime);
                needs.Fatigue01 = MathF.Min(1f, needs.Fatigue01 + needs.FatigueRate * tick.DeltaTime);

                // edge trigger: evento solo se "entra" nello stato hungry
                bool nowHungry = needs.Hunger01 > HungryThreshold;
                if (nowHungry && !needs.IsHungry)
                    bus.Publish(new NpcBecameHungry(id));

                // aggiorna stato
                needs.IsHungry = nowHungry;

                world.Needs[id] = needs;
                updated++;
            }

            telemetry.Counter("NeedsSystem.UpdatedNpcs", updated);
        }
    }
}

