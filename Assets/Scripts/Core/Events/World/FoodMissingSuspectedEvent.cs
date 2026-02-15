namespace Arcontio.Core
{
    /// <summary>
    /// FoodMissingSuspectedEvent (Day9 - rumor/sospetto):
    /// La vittima nota un delta negativo nel proprio cibo privato e sospetta un furto.
    ///
    /// NON è "verità del mondo" su chi sia il ladro: è un sospetto.
    /// - reliability bassa in memoria
    /// - subjectId può essere 0 (sconosciuto)
    /// </summary>
    public readonly struct FoodMissingSuspectedEvent : ISimEvent
    {
        public readonly int VictimNpcId;
        public readonly int MissingUnits;

        // opzionale: dove si trovava la vittima quando lo nota (per contestualizzare)
        public readonly int CellX;
        public readonly int CellY;

        public FoodMissingSuspectedEvent(int victimNpcId, int missingUnits, int cellX, int cellY)
        {
            VictimNpcId = victimNpcId;
            MissingUnits = missingUnits;
            CellX = cellX;
            CellY = cellY;
        }

        public override string ToString()
        {
            return $"FoodMissingSuspected victim={VictimNpcId} missing={MissingUnits} at=({CellX},{CellY})";
        }
    }
}
