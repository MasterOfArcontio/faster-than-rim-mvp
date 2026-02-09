namespace Arcontio.Core
{
    /// <summary>
    /// WorldObjectInstance:
    /// Istanza nel mondo di un ObjectDef.
    ///
    /// Esempio:
    /// - DefId = "bed_wood_poor"
    /// - In cella (x,y)
    /// - owner = Npc 12
    /// - occupant = npc che sta dormendo (solo per letti)
    /// </summary>
    public sealed class WorldObjectInstance
    {
        public int ObjectId;        // id numerico runtime (univoco)
        public string DefId;        // chiave su world.ObjectDefs

        public int CellX;
        public int CellY;

        // Ownership logica
        public OwnerKind OwnerKind;
        public int OwnerId;         // valido se OwnerKind != None

        // Stato runtime (specifico per alcuni oggetti)
        public int OccupantNpcId;   // letto: chi lo occupa, -1 se libero

        public WorldObjectInstance()
        {
            OccupantNpcId = -1;
            OwnerKind = OwnerKind.None;
            OwnerId = -1;
        }

        public override string ToString()
        {
            return $"obj#{ObjectId} def={DefId} cell=({CellX},{CellY}) owner={OwnerKind}:{OwnerId} occ={OccupantNpcId}";
        }
    }
}
