using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace SocialViewer.UI.Overlay
{
    /// <summary>
    /// Gestisce un menu contestuale (tasto destro) in OverlayUI.
    ///
    /// Caratteristiche:
    /// - Si apre in posizione mouse (screen position)
    /// - Genera dinamicamente una lista di bottoni (azioni)
    /// - Attiva un ModalBlocker per bloccare input sul grafo finché il menu è aperto
    /// - Può essere “riempito” con azioni diverse in base al nodo selezionato
    /// </summary>
    public class ContextMenuController : MonoBehaviour
    {
        [Header("Riferimenti UI (da Inspector)")]

        [Tooltip("Pannello principale del menu (es. ContextMenuPanel).")]
        [SerializeField] private RectTransform menuPanel;

        [Tooltip("Prefab di un bottone TMP (UI > Button - TextMeshPro), usato per creare voci del menu.")]
        [SerializeField] private Button buttonPrefab;

        [Tooltip("Contenitore dove instanziare i bottoni (es. ButtonsRoot dentro il menu panel).")]
        [SerializeField] private RectTransform buttonsRoot;

        [Tooltip("GameObject che blocca input quando il menu è aperto (Panel con Image Raycast Target ON).")]
        [SerializeField] private GameObject modalBlocker;

        [Header("Clamp ai bordi")]

        [Tooltip("Se assegnato, clamp del menu per non uscire dallo schermo.")]
        [SerializeField] private RectTransform clampRoot;

        // Canvas di riferimento per conversioni coordinate
        private Canvas _canvas;

        // Nodo “corrente” per cui il menu è aperto
        private int _currentNodeId = -1;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            Hide();
        }
         
        /// <summary>
        /// Apre il menu per un dato nodeId, posizionandolo vicino al puntatore.
        /// screenPosition è in coordinate schermo (pixel).
        /// </summary>
        public void ShowForNode(int nodeId, Vector2 screenPosition)
        {
            _currentNodeId = nodeId;

            // Ricostruisce tutte le voci menu per questo nodo
            BuildMenuFor(nodeId);

            // Posiziona il pannello in overlay in base al mouse
            PositionAt(screenPosition);

            // Attiva blocker e pannello
            if (modalBlocker != null) modalBlocker.SetActive(true);
            if (menuPanel != null) menuPanel.gameObject.SetActive(true);
        }

        /// <summary>
        /// Chiude il menu, disattiva blocker, pulisce i bottoni.
        /// </summary>
        public void Hide()
        {
            _currentNodeId = -1;

            if (menuPanel != null) menuPanel.gameObject.SetActive(false);
            if (modalBlocker != null) modalBlocker.SetActive(false);

            ClearButtons();
        }

        /// <summary>
        /// Costruisce le voci del menu per il nodo selezionato.
        /// Al momento è un esempio: in futuro qui colleghi azioni reali (Focus, Create Edge, Inspect, ecc.).
        /// </summary>
        private void BuildMenuFor(int nodeId)
        {
            ClearButtons();

            // ESEMPIO: sostituisci con azioni vere del tuo tool
            AddAction($"Inspect NPC {nodeId}", () =>
            {
                Debug.Log($"[ContextMenu] Inspect node {nodeId}");
                Hide();
            });

            AddAction("Pin / Follow", () =>
            {
                Debug.Log($"[ContextMenu] Pin node {nodeId}");
                Hide();
            });

            AddAction("Close", () => Hide());
        }

        /// <summary>
        /// Aggiunge una voce (bottone) al menu.
        /// - Istanzia il prefab
        /// - Setta il testo
        /// - Collega callback onClick
        /// </summary>
        private void AddAction(string label, System.Action onClick)
        {
            if (buttonPrefab == null || buttonsRoot == null) return;

            var b = Instantiate(buttonPrefab, buttonsRoot);

            // Recuperiamo il testo TMP figlio del bottone e settiamo la label
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = label;

            // Colleghiamo l'azione al click
            b.onClick.AddListener(() => onClick?.Invoke());
        }

        /// <summary>
        /// Elimina tutti i bottoni attualmente presenti nel menu.
        /// </summary>
        private void ClearButtons()
        {
            if (buttonsRoot == null) return;

            for (int i = buttonsRoot.childCount - 1; i >= 0; i--)
                Destroy(buttonsRoot.GetChild(i).gameObject);
        }

        /// <summary>
        /// Posiziona il menu in anchoredPosition a partire da una screenPosition (mouse).
        /// Poi clamp ai bordi del canvas.
        /// </summary>
        private void PositionAt(Vector2 screenPos)
        {
            if (_canvas == null || menuPanel == null) return;

            RectTransform canvasRect = _canvas.transform as RectTransform;

            // Convertiamo screen -> local (anchored) nel canvas
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                _canvas.worldCamera,
                out var anchored
            );

            // Prima posizione “grezza”
            menuPanel.anchoredPosition = anchored;

            // Forziamo layout (se usi VerticalLayoutGroup + ContentSizeFitter)
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuPanel);

            // Clamp per non uscire dallo schermo
            if (clampRoot != null)
                menuPanel.anchoredPosition = ClampPanel(menuPanel, canvasRect);
        }

        /// <summary>
        /// Clamp della posizione del pannello: mantiene il menu dentro il canvas.
        /// Tiene conto del pivot del pannello.
        /// </summary>
        private static Vector2 ClampPanel(RectTransform panel, RectTransform canvasRect)
        {
            Vector2 pos = panel.anchoredPosition;
            Vector2 size = panel.rect.size;

            // Min/max del canvas in coordinate locali
            // Il pivot influisce sul punto “ancorato”.
            Vector2 min = canvasRect.rect.min + new Vector2(size.x * panel.pivot.x, size.y * panel.pivot.y);
            Vector2 max = canvasRect.rect.max - new Vector2(size.x * (1f - panel.pivot.x), size.y * (1f - panel.pivot.y));

            pos.x = Mathf.Clamp(pos.x, min.x, max.x);
            pos.y = Mathf.Clamp(pos.y, min.y, max.y);

            return pos;
        }  
    }
}
