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
                Debug.Log($"[Event] PredatorSpotted spotter={p.SpotterNpcId} predator={p.PredatorId} cell=({p.CellX},{p.CellY}) q={p.SpotQuality01:0.00}");
                return;
            }

            if (e is AttackEvent a)
            {
                Debug.Log($"[Event] Attack attacker={a.AttackerId} defender={a.DefenderId} cell=({a.CellX},{a.CellY}) dmg={a.DamageAmount}");
                return;
            }

            if (e is DeathEvent d)
            {
                Debug.Log($"[Event] Death victim={d.VictimId} cell=({d.CellX},{d.CellY}) cause={d.Cause} killer={d.KillerId}");
                return;
            }

            if (e is NpcWasFed fed)
            {
                Debug.Log($"[Event] NpcWasFed npc={fed.NpcId} used={fed.UsedFood} hungerAfter={fed.HungerAfter:0.00}");
                return;
            }
        }
    }
}
