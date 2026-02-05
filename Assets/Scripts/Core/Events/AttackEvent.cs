namespace Arcontio.Core
{
    /// <summary>
    /// AttackEvent: un'entità attacca un'altra.
    /// Evento fattuale, non normativo.
    /// </summary>
    public sealed class AttackEvent : IWorldEvent
    {
        public readonly int AttackerId;
        public readonly int DefenderId;
        public readonly int CellX;
        public readonly int CellY;

        // Danno semplice (0 = miss / threat)
        public readonly int DamageAmount;

        public AttackEvent(int attackerId, int defenderId, int cellX, int cellY, int damageAmount)
        {
            AttackerId = attackerId;
            DefenderId = defenderId;
            CellX = cellX;
            CellY = cellY;
            DamageAmount = damageAmount;
        }
    }
}

