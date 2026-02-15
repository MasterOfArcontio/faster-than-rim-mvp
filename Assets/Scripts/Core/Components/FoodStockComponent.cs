using System;

namespace Arcontio.Core
{
    /// <summary>
    /// FoodStockComponent (Day9):
    /// Rappresenta "cibo in stock" associato a un WorldObjectInstance.
    ///
    /// - Units: quantità intera (es. 1 unit = 1 porzione)
    /// - OwnerKind/OwnerId: proprietà dello stock
    ///   * Community/0 => libero
    ///   * Npc/<id>   => privato
    /// </summary>
    public struct FoodStockComponent
    {
        public int Units;

        public OwnerKind OwnerKind;
        public int OwnerId;

        public bool IsEmpty => Units <= 0;

        public bool IsOwnedByNpc(int npcId) => OwnerKind == OwnerKind.Npc && OwnerId == npcId;
        public bool IsCommunity => OwnerKind == OwnerKind.Community;
    }
}