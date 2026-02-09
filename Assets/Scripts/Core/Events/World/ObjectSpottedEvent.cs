namespace Arcontio.Core
{
    /// <summary>
    /// ObjectSpottedEvent (IWorldEvent):
    /// Un NPC vede un oggetto del mondo (letto/workbench/sedia...).
    ///
    /// Perché serve:
    /// - Evita telepatia: la conoscenza "esiste un letto" nasce solo se lo vedi.
    /// - Alimenta memoria tramite MemoryEncodingSystem.
    ///
    /// Nota:
    /// - witnessQuality01 viene da perception (distanza + orientamento + eventuale LOS futuro).
    /// </summary>
    public sealed class ObjectSpottedEvent : IWorldEvent
    {
        public readonly int ObserverNpcId;
        public readonly int ObjectId;
        public readonly string DefId;

        public readonly int CellX;
        public readonly int CellY;

        public readonly float WitnessQuality01;

        public ObjectSpottedEvent(int observerNpcId, int objectId, string defId, int cellX, int cellY, float witnessQuality01)
        {
            ObserverNpcId = observerNpcId;
            ObjectId = objectId;
            DefId = defId;
            CellX = cellX;
            CellY = cellY;
            WitnessQuality01 = witnessQuality01;
        }
    }
}
