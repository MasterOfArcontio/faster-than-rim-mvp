namespace Arcontio.Core
{
    /// <summary>
    /// IWorldEvent:
    /// Marker per eventi "di mondo" (fatti accaduti nel mondo simulato).
    ///
    /// Differenza rispetto a ISimEvent:
    /// - ISimEvent: qualunque evento interno (debug, telemetria, low-level).
    /// - IWorldEvent: solo eventi che possono creare memoria, comunicazione, reputazione, ecc.
    ///
    /// Nota:
    /// - È un marker (nessun metodo) per tenere semplice il bus.
    /// </summary>
    public interface IWorldEvent : ISimEvent { }
}
