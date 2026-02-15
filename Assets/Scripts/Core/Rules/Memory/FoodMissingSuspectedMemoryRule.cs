namespace Arcontio.Core
{
    /// <summary>
    /// FoodMissingSuspectedMemoryRule (Day9):
    /// Codifica un sospetto in memoria: "mi manca cibo, qualcuno potrebbe aver rubato".
    ///
    /// - Solo la vittima registra.
    /// - Reliability bassa (sospetto).
    /// - SubjectId = 0 (ignoto) in v0.
    /// </summary>
    public sealed class FoodMissingSuspectedMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is FoodMissingSuspectedEvent;

        public bool TryEncode(World world, int witnessNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace outTrace)
        {
            outTrace = default;

            var ev = (FoodMissingSuspectedEvent)e;

            if (witnessNpcId != ev.VictimNpcId)
                return false;

            outTrace = new MemoryTrace
            {
                Type = MemoryType.FoodMissingSuspected,
                SubjectId = 0, // ignoto

                CellX = ev.CellX,
                CellY = ev.CellY,

                Intensity01 = 0.45f,
                Reliability01 = 0.25f,   // sospetto => bassa
                DecayPerTick01 = 0.0035f,

                IsHeard = false,
                HeardKind = HeardKind.None,
                SourceSpeakerId = 0
            };

            return true;
        }
    }
}
