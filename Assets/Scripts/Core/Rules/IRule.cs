using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// Rule: logica ad alto livello guidata da eventi.
    /// 
    /// - Consuma eventi dal bus
    /// - Produce comandi
    /// - Può anche pubblicare nuovi eventi (se serve)
    /// </summary>
    public interface IRule
    {
        void Handle(World world, ISimEvent e, List<ICommand> outCommands, Telemetry telemetry);
    }

    /// <summary>
    /// Esempio: quando un NPC ha fame, se c'è cibo, genera un comando di feed.
    /// In futuro sarà mediato da politica/leggi/privilegi, non automatico.
    /// </summary>
    public sealed class BasicSurvivalRule : IRule
    {
        public void Handle(World world, ISimEvent e, List<ICommand> outCommands, Telemetry telemetry)
        {
            if (e is NpcBecameHungry hungry)
            {
                if (world.Global.FoodStock > 0)
                {
                    outCommands.Add(new FeedNpcCommand(hungry.NpcId, 1));
                    telemetry.Counter("Rule.BasicSurvivalRule.FeedIssued", 1);
                }
                else
                {
                    // Se manca cibo, segnaliamo shortage (esempio)
                    outCommands.Add(new NoOpCommand()); // placeholder
                }
            }
        }
    }

    public sealed class NoOpCommand : ICommand
    {
        public void Execute(World world, MessageBus bus) { }
    }
}
