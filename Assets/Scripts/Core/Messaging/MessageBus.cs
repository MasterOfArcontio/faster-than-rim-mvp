using Arcontio.Core;
using System;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// MessageBus: coda di eventi interni al simulatore.
    /// 
    /// Scopo:
    /// - i Systems pubblicano eventi (es. "NpcStarving", "LawBroken", "LeaderChallenged")
    /// - le Rules (alto livello) reagiscono a eventi e generano comandi
    /// - separa calcolo (systems) da decisione/plot (rules)
    /// Giorno 4:
    /// - introduciamo DrainTo(...) per poter "tappare" gli eventi:
    ///   li estraiamo in una lista per processing (es. MemoryEncodingSystem),
    ///   poi possiamo ripubblicarli nel bus per le Rule.
    /// </summary>
    public sealed class MessageBus
    {
        private readonly Queue<ISimEvent> _queue = new();

        public void Publish(ISimEvent e) => _queue.Enqueue(e);

        public bool TryDequeue(out ISimEvent e)
        {
            if (_queue.Count > 0)
            {
                e = _queue.Dequeue();
                return true;
            }
            e = default;
            return false;
        }

        public int Count => _queue.Count;

        /// <summary>
        /// Sposta TUTTI gli eventi attualmente nel bus dentro una lista.
        /// - La lista viene prima svuotata
        /// - Il bus resta vuoto
        ///
        /// Uso tipico:
        /// - StepOneTick drena eventi in buffer
        /// - MemoryEncodingSystem processa buffer e genera memorie
        /// - StepOneTick ripubblica buffer nel bus e lascia lavorare le rules
        /// </summary>
        public void DrainTo(List<ISimEvent> outEvents)
        {
            outEvents.Clear();
            while (TryDequeue(out var e))
                outEvents.Add(e);
        }
    }

    /// <summary>
    /// Marker interface per eventi.
    /// </summary>
    public interface ISimEvent { }

    // Esempi di eventi
    public sealed class NpcBecameHungry : ISimEvent
    {
        public readonly int NpcId;
        public NpcBecameHungry(int npcId) { NpcId = npcId; }
    }

    public sealed class ResourceShortage : ISimEvent
    {
        public readonly string ResourceName;
        public ResourceShortage(string resourceName) { ResourceName = resourceName; }
    }

    public sealed class NpcWasFed : ISimEvent
    {
        public readonly int NpcId;
        public readonly int UsedFood;
        public readonly float HungerAfter;

        public NpcWasFed(int npcId, int usedFood, float hungerAfter)
        {
            NpcId = npcId;
            UsedFood = usedFood;
            HungerAfter = hungerAfter;
        }
    }
}

namespace Arcontio.Core
{
    /// <summary>
    /// TickPulseEvent: classe per gestire in debug che la simulazione proceda senza intoppi regolarmente (quando
    /// avremo eventi molto frequenti e robusti, non servirà più). E' una sorta di "heartbeat" di debug
    /// </summary>
    public sealed class TickPulseEvent : ISimEvent
    {
        public readonly long TickIndex;
        public TickPulseEvent(long tickIndex) { TickIndex = tickIndex; }
    }
}