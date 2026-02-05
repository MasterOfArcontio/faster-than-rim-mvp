namespace Arcontio.Core
{
    /// <summary>
    /// Crea una traccia AttackWitnessed quando un NPC osserva un AttackEvent.
    /// </summary>
    public sealed class AttackWitnessedMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is AttackEvent;

        public bool TryEncode(World world, int observerNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace trace)
        {
            trace = default;
            var ae = (AttackEvent)e;

            // Se l'observer è l'attaccante o il difensore, in futuro useremo tracce diverse.
            // Oggi: se è uno dei due, rendiamo intensità più alta.
            float baseIntensity = 0.50f;
            if (observerNpcId == ae.AttackerId || observerNpcId == ae.DefenderId)
                baseIntensity = 0.75f;

            float reliability = witnessQuality01;
            if (reliability < 0f) reliability = 0f;
            if (reliability > 1f) reliability = 1f;

            // SubjectId: per semplicità memorizziamo l'attaccante come "soggetto"
            trace = new MemoryTrace
            {
                Type = MemoryType.AttackWitnessed,
                SubjectId = ae.AttackerId,
                CellX = ae.CellX,
                CellY = ae.CellY,
                Intensity01 = baseIntensity,
                Reliability01 = reliability,
                DecayPerTick01 = 0.0040f,
                // Gestione dell'origine della notizia
                IsHeard = false,
                HeardKind = HeardKind.None,
                SourceSpeakerId = 0
            };

            return true;
        }
    }
}
