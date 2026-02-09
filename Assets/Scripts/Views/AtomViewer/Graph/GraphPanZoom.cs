using UnityEngine;
using UnityEngine.EventSystems;

namespace SocialViewer.UI.Graph
{
    /// <summary>
    /// Smooth pan & zoom a large RectTransform (GraphContent) inside a masked viewport.
    /// Controls:
    /// - RMB drag: pan (smooth)
    /// - Mouse wheel: zoom toward cursor (smooth)
    /// </summary>
    public class GraphPanZoom : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        [Header("References")]
        [SerializeField] private RectTransform viewport;   // GraphViewport
        [SerializeField] private RectTransform content;    // GraphContent

        [Header("Pan")]
        [SerializeField] private float panSpeed = 1.0f;
        [SerializeField] private float panSmoothTime = 1.40f;
        [SerializeField] private float maxPanSpeed = 6000f;

        [Header("Zoom")]
        [SerializeField] private float zoomStep = 0.08f;
        [SerializeField] private float minZoom = 0.25f;
        [SerializeField] private float maxZoom = 2.5f;
        [SerializeField] private float zoomSmoothTime = 0.10f;

        private bool _isPanning;

        // Targets
        private Vector2 _targetPos;
        private float _targetZoom = 1f;

        // Current (smoothed)
        private Vector2 _posVelocity;
        private float _zoomVelocity;

        private void Reset()
        {
            viewport = GetComponent<RectTransform>();
        }

        private void Awake()
        {
            if (viewport == null) viewport = GetComponent<RectTransform>();

            _targetPos = content.anchoredPosition;
            _targetZoom = content.localScale.x;
        }

        private void Update()
        {
            // Smooth position
            content.anchoredPosition = Vector2.SmoothDamp(
                content.anchoredPosition,
                _targetPos,
                ref _posVelocity,
                panSmoothTime,
                maxPanSpeed,
                Time.unscaledDeltaTime
            );

            // Smooth zoom
            float current = content.localScale.x;
            float z = Mathf.SmoothDamp(
                current,
                _targetZoom,
                ref _zoomVelocity,
                zoomSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            content.localScale = new Vector3(z, z, 1f);
        }

        /// <summary>
        /// Stops inertia immediately and syncs targets to current state.
        /// Call this when starting a node-drag, for example.
        /// </summary>
        public void FreezeMotion()
        {
            _targetPos = content.anchoredPosition;
            _posVelocity = Vector2.zero;

            _targetZoom = content.localScale.x;
            _zoomVelocity = 0f;

            _isPanning = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isPanning = (eventData.button == PointerEventData.InputButton.Right);
            if (_isPanning)
                _posVelocity = Vector2.zero;
            GraphInteractionState.IsPanning = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Hard filter: pan ONLY with RMB drag events
            if (eventData.button != PointerEventData.InputButton.Right)
                return;

            if (!_isPanning)
                return;

            float scale = Mathf.Max(0.0001f, content.localScale.x);
            Vector2 delta = (eventData.delta / scale) * panSpeed;

            _targetPos += delta;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isPanning = false;
            GraphInteractionState.IsPanning = false;
        }

        public void SetInputEnabled(bool enabled)
        {
            if (!enabled)
                FreezeMotion();   // ferma ogni inerzia prima di bloccare
            this.enabled = enabled;
        }

        public void OnScroll(PointerEventData eventData)
        {
            float scroll = eventData.scrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;

            float currentZoom = content.localScale.x;
            float desired = currentZoom * (1f + scroll * zoomStep);
            desired = Mathf.Clamp(desired, minZoom, maxZoom);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    viewport, eventData.position, eventData.pressEventCamera, out var mouseLocalInViewport))
                return;

            Vector2 currentPos = content.anchoredPosition;
            Vector2 pivotToMouse = mouseLocalInViewport - currentPos;
            float ratio = desired / Mathf.Max(0.0001f, currentZoom);

            _targetZoom = desired;
            _targetPos = mouseLocalInViewport - pivotToMouse * ratio;

            _zoomVelocity = 0f;
        }
    }
}


