namespace Arcontio.Core
{
    /// <summary>
    /// PersonalityMemoryParams: tratti "intrinseci" dell'NPC che influenzano
    /// come la memoria viene mantenuta nel tempo e quanto crede ai rumor.
    ///
    /// Importante:
    /// - Non sono "stato" (non cambiano ogni tick normalmente).
    /// - Sono parametri stabili del personaggio.
    ///
    /// In Giorno 3 li usiamo per modulare il DECAY.
    /// In Giorno 4 li useremo anche per modulare la CODIFICA (salience).
    /// </summary>
    public struct PersonalityMemoryParams
    {
        /// <summary>
        /// Sensibilità al trauma: eventi violenti pesano di più.
        /// (Giorno 4: entra nell'encoding, non nel decay base)
        /// </summary>
        public float TraumaSensitivity01;

        /// <summary>
        /// Resilience: più alta => recupera prima => dimentica più in fretta.
        /// Quindi aumenta il decay.
        /// </summary>
        public float Resilience01;

        /// <summary>
        /// Rumination: più alta => "rimugina" => memorie restano vive più a lungo.
        /// Quindi riduce il decay.
        /// </summary>
        public float Rumination01;

        /// <summary>
        /// Gullibility: più alta => accetta rumor poco affidabili.
        /// (Giorno 6–7: entra nell'assimilazione token)
        /// </summary>
        public float Gullibility01;
        
        // Capacità massima di tracce in MemoryStore per NPC.
        public int MaxTraces;

        public static PersonalityMemoryParams DefaultNpc()
        {
            return new PersonalityMemoryParams
            {
                MaxTraces = 128,                    // default ragionevole per test; rendilo config quando vuoi
                TraumaSensitivity01 = 0.50f,
                Resilience01 = 0.50f,
                Rumination01 = 0.25f,
                Gullibility01 = 0.50f
            };
        }
    }
}
