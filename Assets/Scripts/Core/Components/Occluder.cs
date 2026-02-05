namespace Arcontio.Core
{
    /// <summary>
    /// Occluder:
    /// Oggetto "in cella" che può bloccare movimento e/o visione.
    ///
    /// Giorno 7 (TokenDelivery LOS):
    /// - usiamo SOLO BlocksVision per decidere se un token può "passare" in linea retta.
    /// - BlocksMovement e VisionCost sono già qui per i prossimi step (pathfinding/stealth).
    /// </summary>
    public struct Occluder
    {
        public bool BlocksMovement;
        public bool BlocksVision;

        /// <summary>
        /// Quanto "pesa" come ostacolo visivo (0=trasparente, 1=blocco totale).
        /// Giorno 7: usato per degradare i token in delivery (soprattutto AlarmShout)
        /// </summary>
        public float VisionCost;
    }
}
