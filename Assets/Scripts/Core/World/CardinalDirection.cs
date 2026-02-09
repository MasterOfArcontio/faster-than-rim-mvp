namespace Arcontio.Core
{
    /// <summary>
    /// CardinalDirection:
    /// Orientamento discreto su griglia (N/E/S/W).
    ///
    /// Serve per:
    /// - FOV (ObjectPerceptionSystem)
    /// - comunicazione credibile (chi "parla verso" chi)
    /// - futuri sistemi (movimento, combattimento, animazioni)
    /// </summary>
    public enum CardinalDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }
}
