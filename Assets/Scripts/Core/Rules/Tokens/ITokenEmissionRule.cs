using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// ITokenEmissionRule:
    /// Traduce una MemoryTrace in un TokenEnvelope verso un listener.
    ///
    /// Importante (versione pulita):
    /// - Passiamo tickIndex dalla pipeline, così l'envelope è sempre timestampato correttamente
    ///   senza hack su world.Global.
    /// </summary>
    public interface ITokenEmissionRule
    {
        /// <summary>
        /// La rule si applica a questo tipo di trace?
        /// </summary>
        bool Matches(in MemoryTrace trace);

        /// <summary>
        /// Prova a creare un TokenEnvelope (messaggio) verso listener.
        ///
        /// tickIndex:
        /// - tick corrente della simulazione (serve a timestamp e in futuro per decay/cooldown)
        /// </summary>
        bool TryCreateToken(
            World world,
            long tickIndex,
            int speakerNpcId,
            int listenerNpcId,
            in MemoryTrace trace,
            out TokenEnvelope token);
    }
}
