namespace Arcontio.Core
{
    /// <summary>
    /// I comandi sono "intenti" applicabili al World.
    /// 
    /// Differenza con eventi:
    /// - Evento: "è successo X"
    /// - Comando: "fai Y" (modifica il World)
    /// </summary>
    public interface ICommand
    {
        void Execute(World world, MessageBus bus);
    }

    public sealed class FeedNpcCommand : ICommand
    {
        private readonly int _npcId;
        private readonly int _foodAmount;

        public FeedNpcCommand(int npcId, int foodAmount)
        {
            _npcId = npcId;
            _foodAmount = foodAmount;
        }

        public void Execute(World world, MessageBus bus)
        {
            if (!world.ExistsNpc(_npcId)) return;
            if (world.Global.FoodStock <= 0) return;

            int used = _foodAmount;
            if (used > world.Global.FoodStock) used = world.Global.FoodStock;

            world.Global.FoodStock -= used;

            var needs = world.Needs[_npcId];
            needs.Hunger01 = System.MathF.Max(0f, needs.Hunger01 - 0.30f * used);
            world.Needs[_npcId] = needs;

            // Evento "fact" di debug/telemetria: descrive cosa è successo davvero
            bus.Publish(new NpcWasFed(_npcId, used, needs.Hunger01));
        }
    }
}
