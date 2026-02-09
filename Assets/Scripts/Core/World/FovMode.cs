namespace Arcontio.Core
{
    /// <summary>
    /// FovMode:
    /// come calcoliamo il "campo visivo" su griglia.
    ///
    /// - Line: solo la semiretta frontale (stessa riga/colonna davanti)
    /// - Cone: cono discreto (es. 45°) davanti
    /// </summary>
    public enum FovMode
    {
        Line = 0,
        Cone = 1
    }
}
