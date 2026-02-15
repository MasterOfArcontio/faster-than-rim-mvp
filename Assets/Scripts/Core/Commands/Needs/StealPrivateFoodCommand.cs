using Arcontio.Core.Logging;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// StealPrivateFoodCommand (Day9):
    /// Il ladro ruba Units dal cibo privato della vittima.
    ///
    /// Pubblica FoodStolenEvent come FACT del mondo (furto avvenuto).
    /// IMPORTANTE:
    /// - Questo evento NON rende automaticamente la vittima consapevole.
    /// - La consapevolezza/memoria verrà gestita dal MemoryEncodingSystem:
    ///   testimoni (range + cono + LOS) => TheftWitnessed / FoodStolenFromMe
    ///
    /// cellX/cellY dell’evento:
    /// - posizione del ladro al momento dell’azione (se nota),
    /// - fallback (0,0) se mancante.
    /// </summary>
    public sealed class StealPrivateFoodCommand : ICommand
    {
        private readonly int _thiefNpcId;
        private readonly int _victimNpcId;
        private readonly int _units;

        /// <summary>
        /// Overload comodo: units default = 1.
        /// Così NeedsDecisionRule può chiamare new StealPrivateFoodCommand(thief, victim)
        /// senza errori di compilazione.
        /// </summary>
        public StealPrivateFoodCommand(int thiefNpcId, int victimNpcId)
            : this(thiefNpcId, victimNpcId, 1)
        {
        }

        public StealPrivateFoodCommand(int thiefNpcId, int victimNpcId, int units)
        {
            _thiefNpcId = thiefNpcId;
            _victimNpcId = victimNpcId;
            _units = units <= 0 ? 1 : units;
        }

        public void Execute(World world, MessageBus bus)
        {
            if (!world.ExistsNpc(_thiefNpcId) || !world.ExistsNpc(_victimNpcId))
                return;

            if (!world.NpcPrivateFood.TryGetValue(_victimNpcId, out int victimFood) || victimFood <= 0)
                return;

            int stolen = _units;
            if (stolen > victimFood) stolen = victimFood;

            // 1) Mutazione stato: togli cibo alla vittima
            world.NpcPrivateFood[_victimNpcId] = victimFood - stolen;

            // 2) Cella evento = posizione del ladro (se esiste)
            int ex = 0, ey = 0;
            if (world.GridPos.TryGetValue(_thiefNpcId, out var p))
            {
                ex = p.X;
                ey = p.Y;
            }

            // 3) Pubblica FACT del mondo: il furto è successo
            bus.Publish(new FoodStolenEvent(
                victimNpcId: _victimNpcId,
                thiefNpcId: _thiefNpcId,
                units: stolen,
                cellX: ex,
                cellY: ey
            ));
             
            ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "World", cell: (ex, ey)),
                new LogBlock(LogLevel.Info, "log.world.theft.happened")
                    .AddField("thief", _thiefNpcId)
                    .AddField("victim", _victimNpcId)
                    .AddField("units", stolen)
            );
        }
    }
}
