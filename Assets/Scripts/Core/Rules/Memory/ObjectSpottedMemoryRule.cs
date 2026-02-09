namespace Arcontio.Core
{
    /// <summary>
    /// ObjectSpottedMemoryRule:
    /// Quando arriva un ObjectSpottedEvent, crea/rafforza una traccia in memoria
    /// che rappresenta "so che esiste quell'oggetto in quella cella".
    ///
    /// Nota:
    /// - Questo è “vista -> ricordo” (knowledge).
    /// - Per ora lo modelliamo come una MemoryTrace specifica (es. ObjectSpotted / ObjectKnown).
    /// </summary>
    public sealed class ObjectSpottedMemoryRule : IMemoryRule
    {
        public bool Matches(ISimEvent e) => e is ObjectSpottedEvent;

        public bool TryEncode(World world, int observerNpcId, ISimEvent e, float witnessQuality01, out MemoryTrace trace)
        {
            trace = default;

            if (e is not ObjectSpottedEvent ev)
                return false;

            // Safety: observer esiste?
            if (!world.ExistsNpc(observerNpcId))
                return false;

            // Reliability = witnessQuality (clamp)
            // La “conoscenza” di un oggetto di solito non è traumatica:
            // intensità bassa-media, decay lento (conoscenza tende a restare).
            float intensity = 0.25f;
            float reliability = witnessQuality01;
            if (reliability < 0.05f) reliability = 0.05f;

            // Qui scegli tu il MemoryType effettivo.
            // Se non esiste ancora, aggiungilo a MemoryType: es. ObjectSpotted / ObjectKnown.
            trace = new MemoryTrace
            {
                Type = MemoryType.ObjectSpotted, // <-- assicurati che esista nel tuo enum
                SubjectId = ev.ObjectId,         // oppure ev.DefId se preferisci “conoscenza per tipo”
                CellX = ev.CellX,
                CellY = ev.CellY,

                Intensity01 = intensity,         // conoscenza “debole” ma utile
                Reliability01 = reliability,
                DecayPerTick01 = 0.001f,         // lenta: la conoscenza di un letto non sparisce subito

                // Heard flags: qui è DIRETTO (visto), quindi:
                IsHeard = false,
                HeardKind = HeardKind.None,
                SourceSpeakerId = 0
            };

            return true;
        }
    }
}
