using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace SocialViewer.UI.Graph
{
    using UnityEngine;

    public static class NodeCollisionResolver
    {
        public static Vector2 Resolve(
            NPCNodeCollision self,
            Vector2 desiredAnchoredPos,
            float padding = 2f,
            int iterations = 6)
        {
            if (self == null || NodeRegistry.Instance == null) return desiredAnchoredPos;

            Vector2 p = desiredAnchoredPos;
            float rSelf = self.radius;

            var all = NodeRegistry.Instance.AllNodes;
            if (all == null || all.Count == 0) return desiredAnchoredPos;

            for (int it = 0; it < iterations; it++)
            {
                bool moved = false;

                for (int i = 0; i < all.Count; i++)
                {
                    var other = all[i];
                    if (other == null || other == self) continue;

                    Vector2 op = other.Rect.anchoredPosition;
                    float rOther = other.radius;

                    Vector2 delta = p - op;
                    float dist = delta.magnitude;

                    float minDist = rSelf + rOther + padding;

                    if (dist < 0.0001f)
                    {
                        delta = Vector2.right;
                        dist = 0.0001f;
                    }

                    if (dist < minDist)
                    {
                        float push = (minDist - dist);
                        p += (delta / dist) * push;
                        moved = true;
                    }
                }

                if (!moved) break;
            }

            return p;
        }
    }



    /// <summary>
    /// Manual node dragging inside the graph space.
    /// Controls:
    /// - Shift + LMB drag: move node
    /// </summary>
    [DisallowMultipleComponent]
    public class NPCNodeDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Drag rule")]
        [SerializeField] private bool requireShift = true;
        [SerializeField] private PointerEventData.InputButton dragButton = PointerEventData.InputButton.Left;

        [Header("Injected")]
        [SerializeField] private GraphPanZoom panZoom;          // injected
        [SerializeField] private RectTransform dragSpaceRect;   // injected (NodesRoot recommended)

        [Header("Optional")]
        [SerializeField] private Button optionalButtonToDisable;

        private RectTransform _nodeRect;
        private bool _dragging;
        private Vector2 _startMouseLocal;
        private Vector2 _startAnchoredPos;
        private NPCNodeCollision _collision;


        private void Awake()
        {
            _nodeRect = transform as RectTransform;

            _collision = GetComponent<NPCNodeCollision>();

            if (optionalButtonToDisable == null)
                optionalButtonToDisable = GetComponent<Button>();
        }

        /// <summary>
        /// Set delle dipendenze (usato in GraphNodeSpawner quando creo le sfere per ottenere tutte le dipendenze chep poi serviranno
        /// </summary>
        public void SetDependencies(GraphPanZoom graphPanZoom, RectTransform dragSpace)
        {
            panZoom = graphPanZoom;
            dragSpaceRect = dragSpace;
        }

        /// <summary>
        /// Semplice controllo della pressione del tasto SHIFT
        /// </summary>
        private static bool IsShiftHeld()
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = false;

            if (eventData.button != dragButton) return;
            if (requireShift && !IsShiftHeld()) return;

            if (panZoom != null)
                panZoom.SetInputEnabled(false);

            if (dragSpaceRect == null)
            {
                // Fallback: parent space, but you should inject NodesRoot for consistency.
                dragSpaceRect = _nodeRect.parent as RectTransform;
            }

            // Stop any residual inertial motion so it doesn't look like "pan starts"
            if (panZoom != null)
                panZoom.FreezeMotion();

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragSpaceRect, eventData.position, eventData.pressEventCamera, out _startMouseLocal))
                return;

GraphInteractionState.IsDraggingNode = true;

            _startAnchoredPos = _nodeRect.anchoredPosition;
            _dragging = true;

            if (optionalButtonToDisable != null)
                optionalButtonToDisable.interactable = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            if (eventData.button != dragButton) return;
            if (requireShift && !IsShiftHeld()) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragSpaceRect, eventData.position, eventData.pressEventCamera, out var mouseLocal))
                return;

            Vector2 delta = mouseLocal - _startMouseLocal;
            Vector2 desired = _startAnchoredPos + delta;

            Vector2 finalPos = desired;

            // Anti-overlap (se disponibile)
            if (_collision != null)
            {
                finalPos = NodeCollisionResolver.Resolve(_collision, desired, padding: 2f, iterations: 6);

                // Se stai usando la versione semplice di NodeRegistry che ti ho dato, questa chiamata non esiste.
                // Se in futuro aggiungi NotifyMoved, la riattivi.
                // NodeRegistry.Instance?.NotifyMoved(_collision, finalPos);
            }

            _nodeRect.anchoredPosition = finalPos;

            eventData.dragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != dragButton) return;

            if (panZoom != null)
                panZoom.SetInputEnabled(true);
if (GraphInteractionState.IsDraggingNode)
  GraphInteractionState.IsDraggingNode = false;
            _dragging = false;
        }
    }
}
