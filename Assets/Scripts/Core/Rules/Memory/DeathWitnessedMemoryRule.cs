using NUnit;

namespace Arcontio.Core
{
    /// <summary>
    /// Crea una traccia DeathWitnessed quando un NPC osserva un DeathEvent.
    /// </summary>
    public sealed class DeathWitnessedMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is DeathEvent;

        public bool TryEncode(World world, int observerNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace trace)
        {
            trace = default;
            var de = (DeathEvent)e;

            float reliability = witnessQuality01;
            if (reliability < 0f) reliability = 0f;
            if (reliability > 1f) reliability = 1f;

            trace = new MemoryTrace
            {
                Type = MemoryType.DeathWitnessed,
                SubjectId = de.VictimId,
                CellX = de.CellX,
                CellY = de.CellY,
                Intensity01 = 0.85f,     // morte è molto saliente
                Reliability01 = reliability,
                DecayPerTick01 = 0.0015f, // lenta
                // Gestione dell'origine della notizia
                IsHeard = false,
                HeardKind = HeardKind.None,
                SourceSpeakerId = 0
            };

            return true;
        }
    }
}
