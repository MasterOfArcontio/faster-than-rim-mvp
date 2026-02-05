namespace Arcontio.Core
{
    /// <summary>
    /// Crea una traccia PredatorSpotted quando avviene PredatorSpottedEvent.
    /// </summary>
    public sealed class PredatorSpottedMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is PredatorSpottedEvent;

        public bool TryEncode(World world, int observerNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace trace)
        {
            trace = default;

            var pe = (PredatorSpottedEvent)e;

            // L'observer può essere:
            // - lo spotter stesso
            // - un altro NPC che ha visto/udito (oggi trattiamo tutti come "witnessed")
            // La differenziazione "committed/suffered" la facciamo più avanti.

            // Reliability deriva dalla qualità dell'osservazione
            float reliability = witnessQuality01;
            if (reliability < 0f) reliability = 0f;
            if (reliability > 1f) reliability = 1f;

            trace = new MemoryTrace
            {
                Type = MemoryType.PredatorSpotted,
                SubjectId = pe.PredatorId,
                CellX = pe.CellX,
                CellY = pe.CellY,
                Intensity01 = 0.70f,         // base salience (tarabile)
                Reliability01 = reliability,
                DecayPerTick01 = 0.0025f,      // abbastanza lenta (pericolo resta)
                // Gestione dell'origine della notizia
                IsHeard = false,
                HeardKind = HeardKind.None,
                SourceSpeakerId = 0
            };

            return true;
        }
    }
}
