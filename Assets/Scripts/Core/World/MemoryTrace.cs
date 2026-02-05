namespace Arcontio.Core
{
    /// <summary>
    /// MemoryTrace: unità minima di memoria in un NPC.
    /// 
    /// È volutamente "dumb data":
    /// - Tipo: che cosa è successo (MemoryType)
    /// - SubjectId: di chi/che cosa parla (es. predatorId, attackerNpcId, victimNpcId)
    /// - CellX/CellY: dove è accaduto (se rilevante)
    /// - Intensity01: quanto pesa oggi (0..1)
    /// - Reliability01: quanto è affidabile (0..1) (per rumor spesso più bassa)
    /// - DecayPerTick01: quanto cala per tick (0..1 per tick). Il sistema la applica.
    /// </summary>
    public struct MemoryTrace
    {
        public MemoryType Type;

        /// <summary>
        /// Soggetto dell'evento nella memoria (es: predatorId, attackerId, victimId).
        /// -1 se non applicabile.
        /// </summary>
        public int SubjectId;

        /// <summary>
        /// Coordinate della cella a cui la memoria è associata.
        /// -1/-1 se non applicabile.
        /// </summary>
        public int CellX;
        public int CellY;

        /// <summary>
        /// Intensità corrente: 0..1
        /// </summary>
        public float Intensity01;

        /// <summary>
        /// Affidabilità: 0..1
        /// </summary>
        public float Reliability01;

        /// <summary>
        /// Decadimento per tick (base).
        /// Esempio: 0.01 significa che in ~100 tick la traccia si azzera (a parità di dt).
        /// </summary>
        public float DecayPerTick01;

        /// <summary>
        /// Gestione del tipo di fonte: se diretta o raccontata
        /// </summary>       
        public bool IsHeard;
        public HeardKind HeardKind;
        public int SourceSpeakerId; // chi me l'ha detto (solo se IsHeard=true)


        public override string ToString()
        {
            return $"{Type} subj={SubjectId} cell=({CellX},{CellY}) I={Intensity01:0.00} R={Reliability01:0.00} d={DecayPerTick01:0.000}";
        }
    }

    /// <summary>
    /// HeardKind: indica come questa memoria è stata acquisita via comunicazione.
    /// Non è stata quindi acquisita tramite una esperienza diretta
    /// </summary>
    public enum HeardKind
    {
        None = 0,       // memoria non "sentita": esperienza diretta
        DirectHeard = 1, // ricevuta direttamente da uno speaker
        RumorHeard = 2   // informazione di seconda mano (chainDepth > 0)
    }
}
