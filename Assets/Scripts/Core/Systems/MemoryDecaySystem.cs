using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// MemoryDecaySystem: fa decadere le memorie di ogni NPC.
    ///
    /// - Non crea nuove memorie (nessuna codifica eventi qui)
    /// - Non comunica token
    /// - Si limita ad applicare TickDecay su ogni NPC
    /// 
    /// Giorno 3:
    /// - Il decay è modulato dai tratti dell'NPC (PersonalityMemoryParams).
    ///   Resilience alta => dimentica più in fretta
    ///   Rumination alta => dimentica più lentamente
    /// </summary>
    public sealed class MemoryDecaySystem : ISystem
    {
        public int Period => 1;

        private readonly List<int> _ids = new(2048);

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (world.Memory == null || world.Memory.Count == 0)
                return;

            _ids.Clear();
            _ids.AddRange(world.Memory.Keys);

            int removedTotal = 0;

            // Decay scalato dal tempo simulato
            float tickScale = tick.DeltaTime;

            for (int i = 0; i < _ids.Count; i++)
            {
                int id = _ids[i];

                if (!world.Memory.TryGetValue(id, out var store) || store == null)
                    continue;

                // Se non troviamo parametri (non dovrebbe accadere), usiamo default.
                PersonalityMemoryParams p;
                if (!world.MemoryParams.TryGetValue(id, out p))
                    p = PersonalityMemoryParams.DefaultNpc();

                // Calcolo semplice del moltiplicatore di decay:
                // - Resilience aumenta decay (dimentica prima)
                // - Rumination riduce decay (rimugina, trattiene)
                //
                // Esempio:
                //   Resilience 0.0 => +0%
                //   Resilience 1.0 => +100%
                //
                //   Rumination 0.0 => -0%
                //   Rumination 1.0 => -50% (non azzeriamo mai del tutto)
                float decayMultiplier = 1f;

                decayMultiplier += p.Resilience01 * 1.0f;      // +0..+1
                decayMultiplier -= p.Rumination01 * 0.5f;      // -0..-0.5

                // Clamp di sicurezza: non vogliamo decay <= 0
                if (decayMultiplier < 0.10f) decayMultiplier = 0.10f;

                removedTotal += store.TickDecay(tickScale, decayMultiplier);
            }

            telemetry.Counter("MemoryDecaySystem.TracesRemoved", removedTotal);
        }
    }
}
