namespace Arcontio.Core
{
    /// <summary>
    /// MemoryType: tassonomia minima delle tracce di memoria.
    /// 
    /// Nota:
    /// - Questo enum cresce nel tempo.
    /// - È usato come "categoria" della traccia (predatore, violenza, morte, crimine...).
    /// </summary>
    public enum MemoryType
    {
        // Threat / Predator
        PredatorSpotted,
        PredatorRumor,

        // Violence
        AttackSuffered,
        AttackWitnessed,
        NearDeathExperience,

        // Death
        DeathWitnessed,
        DeathOfBonded,
        DeathHeard,

        // NEW (giorno 6)
        AidRequested,

        // NEW (giorno 8 - oggetti)
        ObjectSpotted,

        // Crime (future)
        TheftWitnessed,
        TheftSuffered,
        RobberyWitnessed,
        RobberySuffered,
    }
}
