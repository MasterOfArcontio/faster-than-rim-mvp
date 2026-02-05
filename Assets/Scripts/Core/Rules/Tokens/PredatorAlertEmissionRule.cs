using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// PredatorAlertEmissionRule:
    /// Se uno speaker ha una trace PredatorSpotted (o Threat),
    /// può emettere un token di tipo PredatorAlert/AlarmDanger.
    ///
    /// Versione minima: PredatorSpotted -> PredatorAlert
    /// </summary>
    public sealed class PredatorAlertEmissionRule : ITokenEmissionRule
    {
        public bool Matches(in MemoryTrace trace)
        {
            return trace.Type == MemoryType.PredatorSpotted;
        }

        public bool TryCreateToken(
            World world,
            long tickIndex,
            int speakerNpcId,
            int listenerNpcId,
            in MemoryTrace trace,
            out TokenEnvelope token)
        {
            token = default;

            if (trace.Intensity01 < 0.20f)
                return false;

            // Contenuto simbolico: "predatore qui"
            var symbolic = new SymbolicToken(
                type: TokenType.PredatorAlert,
                subjectId: trace.SubjectId,          // predatorId
                intensity01: trace.Intensity01,
                reliability01: trace.Reliability01,
                chainDepth: 0,
                hasCell: true,
                cellX: trace.CellX,
                cellY: trace.CellY
            );

            //long tickIndex = world.Global.TickIndex;

            token = new TokenEnvelope(
                speakerId: speakerNpcId,
                listenerId: listenerNpcId,
//                channel: TokenChannel.ProximityTalk,
                channel: TokenChannel.AlarmShout,
                tickIndex: tickIndex,
                token: symbolic
            );

            return true;
        }
    }
}
