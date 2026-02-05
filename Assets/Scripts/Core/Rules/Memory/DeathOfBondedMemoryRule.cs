using NUnit;

namespace Arcontio.Core
{
    /// <summary>
    /// DeathOfBondedMemoryRule (STUB):
    /// Se muore un NPC a cui sono legato, crea traccia DeathOfBonded.
    ///
    /// Oggi il controllo dei legami è stub (World.AreBonded ritorna false).
    /// Serve a fissare il contratto e la forma della memoria.
    /// </summary>
    public sealed class DeathOfBondedMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is DeathEvent;

        public bool TryEncode(World world, int observerNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace trace)
        {
            trace = default;
            var de = (DeathEvent)e;

            if (!world.AreBonded(observerNpcId, de.VictimId))
                return false;

            float reliability = witnessQuality01;
            if (reliability < 0f) reliability = 0f;
            if (reliability > 1f) reliability = 1f;

            trace = new MemoryTrace
            {
                Type = MemoryType.DeathOfBonded,
                SubjectId = de.VictimId,
                CellX = de.CellX,
                CellY = de.CellY,
                Intensity01 = 1.0f,     // altissima salience
                Reliability01 = reliability,
                DecayPerTick01 = 0.0008f, // molto lenta
                // Gestione dell'origine della notizia
                IsHeard = false,
                HeardKind = HeardKind.None,
                SourceSpeakerId = 0
            };

            return true;
        }
    }
}
