using UnityEngine;
using UnityEngine.EventSystems;
using SocialViewer.UI.Overlay;
using UnityEngine.InputSystem;

namespace SocialViewer.UI.Graph
{
    public class NPCNodeUIEvents : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [Header("Dipendenze (iniettate)")]
        [SerializeField] private TooltipController tooltip;
        [SerializeField] private ContextMenuController contextMenu;

        [Header("Dati nodo")]
        [SerializeField] private int nodeId;
        [SerializeField] private string nodeName = "NPC";
        [SerializeField] private string nodeState = "Idle";

        [SerializeField] private RectTransform nodeRect; // Il rect del nodo/sfera (di solito il parent)

        [Header("Click vs Drag")]
        [Tooltip("Se il mouse si muove più di questi pixel tra down e up, lo consideriamo drag/pan e NON apriamo il menu.")]
//        [SerializeField] private float rightClickMoveThresholdPx = 12f;
        [SerializeField] private float rightClickThresholdPx = 12f;
        private bool _rmbDownHere;
        private UnityEngine.Vector2 _rmbDownPos;
        private bool _rightDownOnThis;
        private Vector2 _rightDownPos;
        //        private bool _movedTooMuch;

        private void Awake()
        {
            if (nodeRect == null)
                nodeRect = (RectTransform)transform.parent;
        }

        public void SetDependencies(TooltipController t, ContextMenuController c)
        {
            tooltip = t;
            contextMenu = c;
        }

        public void SetNodeInfo(int id, string name, string state)
        {
            nodeId = id;
            nodeName = name;
            nodeState = state;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            //Debug.Log("TOOLTIP?");
            if (tooltip == null)
            {
                Debug.Log("TOOLTIP KO");
                return;
            }

            // Posizione schermo del centro nodo (puoi usare nodeRect.position)
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventData.pressEventCamera, nodeRect.position);

            //Debug.Log("FINO QUI OK");

            tooltip.ShowAt(screen, $"{nodeName}\n{nodeState}");
            //tooltip.Show($"{nodeName}\n{nodeState}");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltip == null) return;
            tooltip.Hide();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Ci interessa solo RMB
            if (eventData.button != PointerEventData.InputButton.Right) return;

            _rightDownOnThis = true;
//            _movedTooMuch = false;
            _rightDownPos = eventData.position; // posizione in pixel
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;

            // Se non era iniziato su questo oggetto, non fare nulla
            if (!_rightDownOnThis) return;

            _rightDownOnThis = false;

            // Se stiamo pannando o trascinando nodi, non aprire menu
            if (GraphInteractionState.IsPanning) return;
            if (GraphInteractionState.IsDraggingNode) return;

            // Se nel frattempo il mouse si è mosso troppo, era pan/drag: non aprire
            //            var delta = eventData.position - _rightDownPos;
            //            if (delta.sqrMagnitude > rightClickMoveThresholdPx * rightClickMoveThresholdPx) return;

            // Apri menu (solo ora, su "right click" reale)
            if (contextMenu != null)
                contextMenu.ShowForNode(nodeId, eventData.position);
        }

        private void Update()
        {
            // Se RMB è giù e muoviamo troppo, segniamo che non è un click.
            if (!_rightDownOnThis) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 now = mouse.position.ReadValue();
            Vector2 delta = now - _rightDownPos;

//            if (delta.sqrMagnitude > rightClickMoveThresholdPx * rightClickMoveThresholdPx)
//                _movedTooMuch = true;
        }
    }
}
