using Arcontio.Core.Diagnostics;
using Arcontio.Core.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Core
{
    public sealed class DebugEventLogRule : IRule
    {
        public void Handle(World world, ISimEvent e, List<ICommand> outCommands, Telemetry telemetry)
        {
            // Qui logghiamo SOLO eventi che stanno nel MessageBus.
            // I token stanno nel TokenBus e vengono loggati da TokenEmissionPipeline o SimulationHost.

            if (e is TickPulseEvent pulse)
            {
//                Debug.Log($"[Pulse] tick={pulse.TickIndex}");
                return;
            }

            if (e is PredatorSpottedEvent p)
            {
                ArcontioLogger.Info(
                    new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "Event", npcId: p.SpotterNpcId, cell: (p.CellX, p.CellY)),
                    new LogBlock(LogLevel.Info, "log.event.predator_spotted")
                        .AddField("predator", p.PredatorId)
                        .AddField("q", p.SpotQuality01.ToString("0.00"))
                );
                return;
            }

            if (e is AttackEvent a)
            {
                ArcontioLogger.Info(
                    new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "Event", npcId: a.AttackerId, cell: (a.CellX, a.CellY)),
                    new LogBlock(LogLevel.Info, "log.event.attack")
                        .AddField("defender", a.DefenderId)
                        .AddField("dmg", a.DamageAmount)
                );
                return;
            }

            if (e is DeathEvent d)
            {
                ArcontioLogger.Info(
                    new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "Event", npcId: d.VictimId, cell: (d.CellX, d.CellY)),
                    new LogBlock(LogLevel.Info, "log.event.death")
                        .AddField("cause", d.Cause)
                        .AddField("killer", d.KillerId)
                );
                return;
            }

            if (e is NpcWasFed fed)
            {
                ArcontioLogger.Info(
                    new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "Event", npcId: fed.NpcId),
                    new LogBlock(LogLevel.Info, "log.event.npc_was_fed")
                        .AddField("used", fed.UsedFood)
                        .AddField("hungerAfter", fed.HungerAfter.ToString("0.00"))
                );
                return;
            }
        }
    }
}
