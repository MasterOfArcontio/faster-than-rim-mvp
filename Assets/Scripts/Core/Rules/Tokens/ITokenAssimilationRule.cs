namespace Arcontio.Core
{
    /// <summary>
    /// ITokenAssimilationRule: trasforma un token ricevuto in:
    /// - una MemoryTrace "heard/rumor"
    /// - e/o una belief stub (per ora ignorata)
    /// 
    /// Nota:
    /// - Non modifica il mondo "oggettivo".
    /// - Modifica solo lo stato mentale del listener (MemoryStore).
    /// </summary>
    public interface ITokenAssimilationRule
    {
        bool Matches(in TokenEnvelope env);

        /// <summary>
        /// Prova ad assimilare il token.
        /// Ritorna true se ha prodotto una traccia (o un effetto).
        /// </summary>
        bool TryAssimilate(World world, in TokenEnvelope env, out MemoryTrace outTrace);
    }
}
