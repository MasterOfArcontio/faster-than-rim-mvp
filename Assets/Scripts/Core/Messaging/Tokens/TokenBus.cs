using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// TokenBus: coda di token (comunicazioni).
    /// Separata dal MessageBus per non mescolare "eventi di mondo" e "messaggi".
    ///
    /// Importante:
    /// - Qui stiamo usando TokenEnvelope come *struct* (value type).
    /// - Quindi NON possiamo usare "null" come valore di fallback.
    /// - Se la coda è vuota, ritorniamo false e mettiamo "default" in out.
    /// </summary>
    public sealed class TokenBus
    {
        private readonly Queue<TokenEnvelope> _queue = new();

        /// <summary>
        /// Enqueue di un envelope.
        /// Nota: TokenEnvelope è uno struct, quindi viene copiato.
        /// Per oggetti piccoli è ok.
        /// </summary>
        public void Publish(TokenEnvelope t) => _queue.Enqueue(t);

        /// <summary>
        /// Dequeue di un envelope.
        /// Ritorna true se esiste un elemento.
        /// Se non esiste, ritorna false e out = default(TokenEnvelope).
        /// </summary>
        public bool TryDequeue(out TokenEnvelope t)
        {
            if (_queue.Count > 0)
            {
                t = _queue.Dequeue();
                return true;
            }

            // TokenEnvelope è uno struct: non può essere null.
            // default(...) mette tutti i campi ai valori di default (0, false, enum=0, ecc.)
            t = default;
            return false;
        }

        public int Count => _queue.Count;

        /// <summary>
        /// Svuota la coda in una lista (riusabile).
        /// Utile per debug/log o per batch processing.
        ///
        /// Nota: la lista contiene struct (copie), quindi nessuna reference condivisa.
        /// </summary>
        public void DrainTo(List<TokenEnvelope> outTokens)
        {
            outTokens.Clear();
            while (TryDequeue(out var t))
                outTokens.Add(t);
        }
    }
}
