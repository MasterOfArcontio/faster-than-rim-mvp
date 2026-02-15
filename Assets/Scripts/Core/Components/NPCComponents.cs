namespace Arcontio.Core
{
    /// <summary>
    /// Dati "identitari" e relativamente stabili dell'NPC.
    /// </summary>
    public struct NpcCore
    {
        public string Name;

        // tratti base (placeholder)
        public float Charisma;
        public float Decisiveness;
        public float Empathy;
        public float Ambition;
    }

    /// <summary>
    /// Bisogni e stati interni che cambiano spesso.
    /// </summary>
    public struct Needs
    {
        public float Hunger01;   // 0=ok, 1=affamato
        public float Fatigue01;  // 0=ok, 1=stanco
        public float Morale01;   // 0=depresso, 1=ottimo

        // timer/accumulatori
        //public float HungerRate;
        //public float FatigueRate;

        // Cache/flag derivati (settati dal NeedsDecaySystem)
        public bool IsHungry;
        public bool IsTired;
    }

    /// <summary>
    /// Stato sociale (placeholder): reputazione, lealtà, legami, ecc.
    /// </summary>
    public struct Social
    {
        public float LeadershipScore;     // "leadership analogica" (non è necessariamente il leader accettato)
        public float LoyaltyToLeader01;   // lealtà verso leader accettato
        public float JusticePerception01; // percezione di giustizia

        // In futuro: relazioni, amicizie, partner, inimicizie, ecc.
    }
}
