namespace Arcontio.Core
{
    /// <summary>
    /// PredatorSpottedEvent: un NPC avvista un predatore.
    /// 
    /// Nota:
    /// - In futuro questo evento potrebbe essere generato da un PredatorPerceptionSystem.
    /// - Oggi lo teniamo come contratto: quando esisterà un predatore, pubblicheremo questo evento.
    /// </summary>
    public sealed class PredatorSpottedEvent : IWorldEvent
    {
        public readonly int SpotterNpcId;
        public readonly int PredatorId;
        public readonly int CellX;
        public readonly int CellY;
        public readonly int DistanceCells;
        public readonly float SpotQuality01;

        public PredatorSpottedEvent(int spotterNpcId, int predatorId, int cellX, int cellY, int distanceCells, float spotQuality01)
        {
            SpotterNpcId = spotterNpcId;
            PredatorId = predatorId;
            CellX = cellX;
            CellY = cellY;
            DistanceCells = distanceCells;
            SpotQuality01 = spotQuality01;
        }
    }
}
