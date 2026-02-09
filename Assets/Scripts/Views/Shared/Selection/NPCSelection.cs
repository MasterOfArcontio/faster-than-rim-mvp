using System;

namespace SocialViewer.UI
{
    /// <summary>
    /// SERVIZIO CENTRALE DI SELEZIONE.
    /// Mantiene la selezione globale senza riferimenti rigidi tra gli oggetti UI.
    /// Classe statica centralizzata per gestire ed evidenziare quale è il NPC selezionato nella view (e altrove)
    /// </summary>
    public static class NPCSelection
    {
        // ID dell'NPC selezionato
        public static int SelectedNpcId { get; private set; } = -1;

        // Esiste un evento pubblico chiamato OnSelectionChanged
        // chiunque può ascoltarlo
        // quando succede, viene passato un int (l’NpcId selezionato)
        public static event Action<int> OnSelectionChanged;

        // Funzione che seleziona l'NPC
        // Quando avviene il cambio di NPC, parte la chiamata a tutti i metodi di tutti gli oggetti
        // che si sono iscritti all'azione OnSelectionChanged
        public static void Select(int npcId)
        {
            // Se l'ID dell'NPC è quello di prima non accade nulla
            if (npcId == SelectedNpcId)
                return;

            // Nuovo ID salvato
            SelectedNpcId = npcId;

            // Chiama tutti i metodi che sono iscritti a questo evento, passando loro l'ID del nuovo nodo selezionato
            // I metodi possono essere di vario oggetti di varie classi
            // Il punto interrogativo serve per fare una invocazione sicura che non dia dei null reference
            OnSelectionChanged?.Invoke(SelectedNpcId);
        }

        public static void Clear()
        {
            Select(-1);
        }
    }
}
