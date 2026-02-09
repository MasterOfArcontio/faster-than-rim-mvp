namespace Arcontio.Core
{
    /// <summary>
    /// OwnerKind:
    /// Chi è il proprietario di un oggetto/risorsa.
    ///
    /// Nota:
    /// - OwnerKind + OwnerId definiscono "owner logico" (legittimità).
    /// - Holder (detentore fisico) per il cibo/stack lo introdurrai quando fai risorse.
    /// </summary><
    public enum OwnerKind
    {
        None,
        Npc,
        Group,
        Community
    }
}
