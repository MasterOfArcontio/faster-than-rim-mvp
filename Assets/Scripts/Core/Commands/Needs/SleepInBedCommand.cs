using Arcontio.Core.Logging;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// SleepInBedCommand (Day9):
    /// L’NPC usa un letto (WorldObjectInstance) tramite ObjectUseState.
    ///
    /// Effetto:
    /// - setta UseState: IsInUse=true, UsingNpcId=npcId
    /// - riduce Fatigue01 usando NeedsConfig.sleepRestGainPerTick
    ///
    /// Nota:
    /// - reasonTag è SOLO per log (non influenza la logica).
    ///   Serve a tenere leggibile il test: "Community" vs "Trespass".
    /// </summary>
    public sealed class SleepInBedCommand : ICommand
    {
        private readonly int _npcId;
        private readonly int _bedObjId;

        // Solo debug/test (facoltativo)
        private readonly string _reasonTag;

        /// <summary>
        /// Firma standard: niente tag, log minimal.
        /// </summary>
        public SleepInBedCommand(int npcId, int bedObjId)
        {
            _npcId = npcId;
            _bedObjId = bedObjId;
            _reasonTag = null;
        }

        /// <summary>
        /// Overload per il test: permette a NeedsDecisionRule di passare "Community"/"Trespass".
        /// </summary>
        public SleepInBedCommand(int npcId, int bedObjId, string reasonTag)
        {
            _npcId = npcId;
            _bedObjId = bedObjId;
            _reasonTag = reasonTag;
        }

        public void Execute(World world, MessageBus bus)
        {
            if (!world.Needs.TryGetValue(_npcId, out var needs))
                return;

            // Letto deve esistere come oggetto
            if (!world.Objects.TryGetValue(_bedObjId, out var obj) || obj == null)
                return;

            // 1) Se già in uso, non facciamo nulla (v0)
            var use = world.GetUseStateOrDefault(_bedObjId);
            if (use.IsInUse)
                return;

            // 2) Occupiamo letto
            use.IsInUse = true;
            use.UsingNpcId = _npcId;
            world.SetUseState(_bedObjId, use);

            // 3) Recupero rest (riduce fatigue)
            var cfg = world.Global.Needs;
            needs.Fatigue01 -= cfg.sleepRestGainPerTick;
            if (needs.Fatigue01 < 0f) needs.Fatigue01 = 0f;

            world.Needs[_npcId] = needs;

            string tag = string.IsNullOrEmpty(_reasonTag) ? "" : $"({_reasonTag}) ";
             
            ArcontioLogger.Debug(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T9", npcId: _npcId),
                new LogBlock(LogLevel.Debug, "log.t9.sleep")
                    .AddField("tag", tag)
                    .AddField("bedObj", _bedObjId)
                    .AddField("fatigueNow", needs.Fatigue01.ToString("0.00"))
            );
        }
    }
}
