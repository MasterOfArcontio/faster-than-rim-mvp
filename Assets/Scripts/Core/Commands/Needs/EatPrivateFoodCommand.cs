using Arcontio.Core.Logging;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// EatPrivateFoodCommand (Day9):
    /// L’NPC consuma 1 unità dal proprio cibo privato (World.NpcPrivateFood[npcId]).
    ///
    /// Effetto:
    /// - decrementa il contatore privato
    /// - riduce Hunger01 usando NeedsConfig.eatSatietyGain
    ///
    /// Nota:
    /// - Non genera di per sé "furto" o "sospetto": è consumo legittimo.
    /// </summary>
    public sealed class EatPrivateFoodCommand : ICommand
    {
        private readonly int _npcId;

        public EatPrivateFoodCommand(int npcId)
        {
            _npcId = npcId;
        }

        public void Execute(World world, MessageBus bus)
        {
            if (!world.Needs.TryGetValue(_npcId, out var needs))
                return;

            if (!world.NpcPrivateFood.TryGetValue(_npcId, out int priv) || priv <= 0)
                return;

            // 1) Mutazione inventario privato
            world.NpcPrivateFood[_npcId] = priv - 1;
            
            // Marker: "ho consumato io" in questo tick
            world.NpcLastPrivateFoodConsumeTick[_npcId] = world.Global.CurrentTickIndex;
            
            // 2) Mutazione hunger
            var cfg = world.Global.Needs;
            needs.Hunger01 -= cfg.eatSatietyGain;
            if (needs.Hunger01 < 0f) needs.Hunger01 = 0f;

            world.Needs[_npcId] = needs;
             
            ArcontioLogger.Debug(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T9", npcId: _npcId),
                new LogBlock(LogLevel.Debug, "log.t9.eat.private")
                    .AddField("privLeft", world.NpcPrivateFood[_npcId])
                    .AddField("hungerNow", needs.Hunger01.ToString("0.00"))
            );
        }
    }
}
