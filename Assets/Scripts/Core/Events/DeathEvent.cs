namespace Arcontio.Core
{
    public enum DeathCause
    {
        Starvation,
        Combat,
        Exposure,
        Unknown
    }

    /// <summary>
    /// DeathEvent: un'entità muore (vivo -> morto).
    /// </summary>
    public sealed class DeathEvent : IWorldEvent
    {
        public readonly int VictimId;
        public readonly int CellX;
        public readonly int CellY;
        public readonly DeathCause Cause;

        // killer opzionale (0 o -1 se ignoto/non applicabile)
        public readonly int KillerId;

        public DeathEvent(int victimId, int cellX, int cellY, DeathCause cause, int killerId = -1)
        {
            VictimId = victimId;
            CellX = cellX;
            CellY = cellY;
            Cause = cause;
            KillerId = killerId;
        }
    }
}
