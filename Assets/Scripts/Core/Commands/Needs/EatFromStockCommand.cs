using Arcontio.Core.Logging;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// EatFromStockCommand (Day9):
    /// Consuma 1 unità da uno stock di cibo (oggetto in mondo).
    ///
    /// Effetto:
    /// - decrementa FoodStockComponent.Units
    /// - riduce Hunger01 usando NeedsConfig.eatSatietyGain
    ///
    /// Nota:
    /// - Non pubblichiamo eventi qui per ora (scelta minimal).
    /// - Se vuoi: puoi pubblicare un evento FoodConsumedEvent per memorie/telemetria.
    /// </summary>
    public sealed class EatFromStockCommand : ICommand
    {
        private readonly int _npcId;
        private readonly int _foodObjId;

        public EatFromStockCommand(int npcId, int foodObjId)
        {
            _npcId = npcId;
            _foodObjId = foodObjId;
        }

        public void Execute(World world, MessageBus bus)
        {
            if (!world.Needs.TryGetValue(_npcId, out var needs))
                return;

            if (!world.FoodStocks.TryGetValue(_foodObjId, out var stock))
                return;

            if (stock.Units <= 0)
                return;

            // 1) Mutazione stock
            stock.Units -= 1;

            bool depleted = stock.Units <= 0;

            if (depleted)
            {
                // Rimuovi l'istanza dal mondo + componenti collegati
                world.FoodStocks.Remove(_foodObjId);
                world.ObjectUse.Remove(_foodObjId);
                world.Objects.Remove(_foodObjId);
            }
            else
            {
                world.FoodStocks[_foodObjId] = stock;
            }

            // 2) Mutazione hunger
            var cfg = world.Global.Needs;
            needs.Hunger01 -= cfg.eatSatietyGain;
            if (needs.Hunger01 < 0f) needs.Hunger01 = 0f;
            world.Needs[_npcId] = needs;

            ArcontioLogger.Debug(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T9", npcId: _npcId),
                new LogBlock(LogLevel.Debug, "log.t9.eat.stock")
                    .AddField("obj", _foodObjId)
                    .AddField("stockLeft", depleted ? 0 : stock.Units)
                    .AddField("hungerNow", needs.Hunger01.ToString("0.00"))
                    .AddField("depleted", depleted.ToString())
            );
        }
    }
}
