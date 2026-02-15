namespace Arcontio.Core
{
    /// <summary>
    /// FoodStolenEvent (Day9):
    /// Evento di furto cibo (FACT del mondo).
    ///
    /// IMPORTANTISSIMO (versione rumor/sospetto):
    /// - Questo evento descrive che il furto è avvenuto davvero nel mondo.
    /// - NON implica che la vittima lo sappia automaticamente.
    /// - La "conoscenza" verrà gestita in MemoryEncodingSystem (testimoni) + sistemi di sospetto.
    ///
    /// victimNpcId: a chi è stato rubato
    /// thiefNpcId: chi ha rubato
    /// units: quante unità
    /// cellX/cellY: dove è avvenuto (per percezione/LOS/range)
    /// </summary>
    public readonly struct FoodStolenEvent : ISimEvent
    {
        public readonly int VictimNpcId;
        public readonly int ThiefNpcId;
        public readonly int Units;

        public readonly int CellX;
        public readonly int CellY;

        public FoodStolenEvent(int victimNpcId, int thiefNpcId, int units, int cellX, int cellY)
        {
            VictimNpcId = victimNpcId;
            ThiefNpcId = thiefNpcId;
            Units = units;

            CellX = cellX;
            CellY = cellY;
        }

        public override string ToString()
        {
            return $"FoodStolen victim={VictimNpcId} thief={ThiefNpcId} units={Units} at=({CellX},{CellY})";
        }
    }
}
