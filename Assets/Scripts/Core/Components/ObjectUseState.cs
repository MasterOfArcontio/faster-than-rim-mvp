namespace Arcontio.Core
{
    /// <summary>
    /// ObjectUseState (Day9):
    /// Stato runtime di utilizzo di un oggetto (letto, workbench...).
    ///
    /// v0:
    /// - Usato per "letto occupato" nel test.
    /// - L'oggetto è identificato dal suo objectId nel World.
    /// </summary>
    public struct ObjectUseState
    {
        public bool IsInUse;
        public int UsingNpcId;

        public static ObjectUseState Free() => new ObjectUseState { IsInUse = false, UsingNpcId = 0 };
    }
}
