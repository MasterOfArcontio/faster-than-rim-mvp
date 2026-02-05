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

        // Crime (future)
        TheftWitnessed,
        TheftSuffered,
        RobberyWitnessed,
        RobberySuffered,

        // Richieste d'aiuto ricevute (token -> trace)
        AidRequested,
    }
}
