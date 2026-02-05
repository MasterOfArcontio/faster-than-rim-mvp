namespace Arcontio.Core
{
    /// <summary>
    /// IMemoryRule: traduce un evento (ISimEvent) in una MemoryTrace per un NPC osservatore.
    ///
    /// Non decide chi osserva: quello lo fa il MemoryEncodingSystem.
    /// Qui si decide solo: "dato observer e evento, che trace creo?"
    /// </summary>
    public interface IMemoryRule
    {
        /// <summary>Ritorna true se questa rule sa gestire questo evento.</summary>
        bool Matches(ISimEvent e);

        /// <summary>
        /// Prova a creare una MemoryTrace.
        /// witnessQuality01: quanto bene l'observer ha percepito (0..1).
        /// </summary>
        bool TryEncode(World world, int observerNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace trace);
    }
}
