namespace Arcontio.Core
{
    /// <summary>
    /// FoodMissingMemoryRule:
    /// Trasforma FoodMissingEvent (deduzione: "mi manca cibo") in una MemoryTrace di SOSPETTO.
    ///
    /// Nota:
    /// - Questa memoria non dice "chi è stato".
    /// - È volutamente a bassa reliability.
    /// - Più avanti potrai farla influenzare da:
    ///   - chi era vicino al deposito
    ///   - reputazione del sospetto
    ///   - storico relazioni
    /// </summary>
    public sealed class FoodMissingMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is FoodMissingEvent;

        public bool TryEncode(World world, int witnessNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace outTrace)
        {
            outTrace = default;

            var ev = (FoodMissingEvent)e;

            // Solo la vittima "deduce" la mancanza.
            if (witnessNpcId != ev.VictimNpcId)
                return false;

            float intensity = 0.30f + 0.10f * ev.MissingUnits; // scala leggermente con units
            if (intensity > 0.65f) intensity = 0.65f;

            outTrace = new MemoryTrace
            {
                Type = MemoryType.FoodPossiblyStolenFromMe, // aggiungila a MemoryType
                SubjectId = 0,      // 0 = ignoto
                CellX = -1,
                CellY = -1,
                Intensity01 = intensity,
                Reliability01 = 0.35f,  // sospetto: bassa
                DecayPerTick01 = 0.0040f,
                IsHeard = false,
                HeardKind = HeardKind.None,
                SourceSpeakerId = 0
            };

            return true;
        }
    }
}
