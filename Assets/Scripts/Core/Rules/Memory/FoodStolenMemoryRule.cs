namespace Arcontio.Core
{
    /// <summary>
    /// FoodStolenMemoryRule (Day9):
    /// Trasforma FoodStolenEvent in memorie, MA SOLO per chi è testimone (perception-based).
    ///
    /// Output:
    /// - Se witnessNpcId == VictimNpcId  -> MemoryType.FoodStolenFromMe
    /// - Se witnessNpcId != VictimNpcId  -> MemoryType.TheftWitnessed
    ///
    /// Importante:
    /// - Questa rule NON decide i testimoni.
    ///   La selezione dei testimoni avviene in MemoryEncodingSystem (range + cone + LOS).
    /// - Se la vittima NON vede, non avrà FoodStolenFromMe (coerente con “rumor/sospetto”).
    /// </summary>
    public sealed class FoodStolenMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is FoodStolenEvent;

        public bool TryEncode(World world, int witnessNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace outTrace)
        {
            outTrace = default;

            var ev = (FoodStolenEvent)e;

            // Se per qualche ragione witnessQuality è bassissimo, puoi anche decidere di non encodare.
            // (Lasciamo passare: MemoryEncodingSystem già filtra per range/LOS.)
            float q = witnessQuality01;
            if (q < 0.05f) q = 0.05f;

            if (witnessNpcId == ev.VictimNpcId)
            {
                // Vittima: “mi hanno rubato” MA SOLO se è testimone.
                outTrace = new MemoryTrace
                {
                    Type = MemoryType.FoodStolenFromMe,

                    // SubjectId = ladro
                    SubjectId = ev.ThiefNpcId,

                    // NEW: SecondarySubjectId = vittima (qui ridondante ma utile se vuoi uniformità)
                    SecondarySubjectId = ev.VictimNpcId,

                    // Cella dove è avvenuto
                    CellX = ev.CellX,
                    CellY = ev.CellY,

                    // Intensità può dipendere dalla qualità witness (più lontano = meno impatto)
                    Intensity01 = 0.65f + 0.25f * q,
                    Reliability01 = 0.80f + 0.20f * q,

                    DecayPerTick01 = 0.0025f,

                    IsHeard = false,
                    HeardKind = HeardKind.None,
                    SourceSpeakerId = 0
                };

                return true;
            }
            else
            {
                // Altri testimoni: “ho visto un furto”
                outTrace = new MemoryTrace
                {
                    Type = MemoryType.TheftWitnessed,

                    // SubjectId = ladro
                    SubjectId = ev.ThiefNpcId,

                    // SecondarySubjectId = vittima (questo è il punto “pulito” che volevi)
                    SecondarySubjectId = ev.VictimNpcId,

                    CellX = ev.CellX,
                    CellY = ev.CellY,

                    // Un testimone tende ad avere intensità un po’ più bassa della vittima
                    Intensity01 = 0.50f + 0.20f * q,
                    Reliability01 = 0.75f + 0.25f * q,

                    DecayPerTick01 = 0.0020f,

                    IsHeard = false,
                    HeardKind = HeardKind.None,
                    SourceSpeakerId = 0
                };

                return true;
            }
        }
    }
}
