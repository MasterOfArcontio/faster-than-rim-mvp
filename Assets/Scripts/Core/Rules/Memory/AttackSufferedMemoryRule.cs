using NUnit;

namespace Arcontio.Core
{
    /// <summary>
    /// AttackSufferedMemoryRule:
    /// Se l'NPC è il defender (vittima) dell'AttackEvent,
    /// crea una traccia AttackSuffered.
    /// </summary>
    public sealed class AttackSufferedMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is AttackEvent;

        public bool TryEncode(World world, int observerNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace trace)
        {
            trace = default;
            var ae = (AttackEvent)e;

            // Questa rule vale SOLO per la vittima.
            if (observerNpcId != ae.DefenderId)
                return false;

            // La vittima ha in genere affidabilità alta (l'ha vissuto).
            // In questa fase manteniamo witnessQuality come input,
            // ma lo clampiamo verso l'alto.
            float reliability = witnessQuality01;
            if (reliability < 0.60f) reliability = 0.60f;
            if (reliability > 1f) reliability = 1f;

            // SubjectId: memorizziamo l'attaccante come "soggetto della minaccia"
            trace = new MemoryTrace
            {
                Type = MemoryType.AttackSuffered,
                SubjectId = ae.AttackerId,
                CellX = ae.CellX,
                CellY = ae.CellY,
                Intensity01 = 0.85f,       // subire attacco è molto saliente
                Reliability01 = reliability,
                DecayPerTick01 = 0.0025f,   // più lenta di AttackWitnessed
                // Gestione dell'origine della notizia
                IsHeard = false,
                HeardKind = HeardKind.None,
                SourceSpeakerId = 0
            };

            return true;
        }
    }
}
