using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SocialViewer.UI
{
    /// <summary>
    /// Componente View/UI per un singolo nodo NPC.
    /// - Display del nome e dello stato TMP.
    /// - Gestisce i clik di selezione.
    /// - Notifica un servizio centralòizzato di selezione (NPCSelection).
    /// </summary>
    ///
    
    // Richiede un componente di tipo Button
    [RequireComponent(typeof(Button))]
    public class NPCNodeView : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image background;     // Immagine di Background
        [SerializeField] private TMP_Text nameLabel;   // NameLabel TMP
        [SerializeField] private TMP_Text stateLabel;  // StateLabel TMP (opzionale ma raccomandato)

        // Facciamo selezionare dall'inspector se cambia colore se selezionato (proprietà usata dopo)
        [Header("Visuals")]
        [SerializeField] private bool useSelectionTint = true;

        // Node identity
        // Proprietà a visibilità pubblica ma setting privato
        public int NpcId { get; private set; } = -1;

        // Opzionale: extra data mostrata in UI
        public string NpcName { get; private set; } = "";
        public string NpcState { get; private set; } = "";

        private Button _button;
        private Color _baseColor;

        private void Awake()
        {
            // Mi prendo il gameobject associato
            _button = GetComponent<Button>();

            // Salvo lo sfondo per dopo
            if (background != null)
                _baseColor = background.color;

            // Keep button behavior normal; selection highlight handled by this script
            // so we don't rely on Button transition states for selection.
        }

        private void OnEnable()
        {
            // Iscrivo il mio metodo HandleSelectionChanged() all'elenco dei metodi che vengono poi chiamati
            // in broadcast da invoke nella classe statica NPCSelection
            NPCSelection.OnSelectionChanged += HandleSelectionChanged;

            //
            HandleSelectionChanged(NPCSelection.SelectedNpcId);
        }

        private void OnDisable()
        {
            // Disiscrivo il mio metodo HandleSelectionChanged() all'elenco dei metodi che vengono poi chiamati
            // in broadcast da invoke nella classe statica NPCSelection
            NPCSelection.OnSelectionChanged -= HandleSelectionChanged;
        }

        /// <summary>
        /// Inizializza o fa il re-bind di questo nodo ad un NPC.
        /// </summary>
        public void Bind(int npcId, string npcName, string npcState)
        {
            // Imposto i dati ID nome e stato del nodo bindato alla view del nodo
            // lo faccio in maniera safe per evitare strani null nelle stringhe
            NpcId = npcId;
            NpcName = npcName ?? "";
            NpcState = npcState ?? "";

            // Se la stringa non è nulla, metto il nome. Se il nome non c'è metto l'ID in formato stringa
            if (nameLabel != null)
                nameLabel.text = string.IsNullOrWhiteSpace(NpcName) ? $"NPC {NpcId}" : NpcName;

            // Aggiorno lo stato del nodo
            if (stateLabel != null)
                stateLabel.text = NpcState;
        }

        /// <summary>
        /// Updatedell'etichetta di stato  (tipicamente durante i tick di simulazione).
        /// </summary>
        public void SetState(string npcState)
        {
            NpcState = npcState ?? "";
            if (stateLabel != null)
                stateLabel.text = NpcState;
        }

        /// <summary>
        /// Click handling (left click selects this NPC).
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            // Se il tasto del mouse premuto è quello sinistro andiamo avanti
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            if (NpcId < 0)
                return;

            // Seleziono il nuovo NPC (e modifico quindi l'ID attivo)
            // Questa chiamata dentro di sè scatena l'invoke in broadcast di tutti i metodi registrati nell'azione
            // della classe statica NPCSelection "OnSelectionChanged".
            // Nel caso di NPCNodeView, registriamo a quella azione il suo metodo HandleSelectionChanged.
            // Questo avviene nell'evento "OnEnable"
            NPCSelection.Select(NpcId);
        }

        /// <summary>
        // Metodo registrato nell'azione della classe statica NPCSelection "OnSelectionChanged".
        // Quando si preme il tasto sinistro del mouse, viene scatenata l'azione che fa l'invoke broadcast che chiama
        // questo metodo per tutti gli oggetti attivi
        /// </summary>
        private void HandleSelectionChanged(int selectedNpcId)
        {
            // Vediamo se l'oggetto è quello selezionato
            bool isSelected = (NpcId >= 0 && NpcId == selectedNpcId);

            // Optional: disable button interaction for selected node (feel free to remove)
            // _button.interactable = !isSelected;

            if (background != null && useSelectionTint)
            {
                // Do not hardcode colors: we do a simple brightening/dimming relative to base color.
                // This avoids "style decisions" and keeps it neutral.
                background.color = isSelected ? Brighten(_baseColor, 0.36f) : _baseColor;
            }

            // Optional: emphasize text weight when selected
            if (nameLabel != null)
                nameLabel.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
        }

        private static Color Brighten(Color c, float amount01)
        {
            // amount01 ~ 0.1-0.25 is a reasonable range
            float r = Mathf.Clamp01(c.r + amount01);
            float g = Mathf.Clamp01(c.g + amount01);
            float b = Mathf.Clamp01(c.b + amount01);
            return new Color(r, g, b, c.a);
        }
    }
}
