using UnityEngine;
using UnityEngine.InputSystem;

namespace Arcontio.Views.MapGrid
{
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraControllerNewInput : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float panSpeed = 0.02f; // scala in unità mondo per pixel di delta
        [SerializeField] private float panSpeedZoomFactor = 0.15f; // pan aumenta con zoom-out

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 0.10f; // sensibilità zoom
        [SerializeField] private float minOrthoSize = 2f;
        [SerializeField] private float maxOrthoSize = 60f;

        private Camera _cam;

        // New Input System actions (create in code, no asset needed)
        private InputAction _panButton;     // RMB
        private InputAction _pointerDelta;  // Pointer delta
        private InputAction _scroll;        // Mouse scroll (Vector2)

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        private void OnEnable()
        {
            // Right mouse button pan
            _panButton = new InputAction("PanButton", InputActionType.Button, "<Mouse>/rightButton");
            _panButton.Enable();

            // Pointer delta (works for mouse)
            _pointerDelta = new InputAction("PointerDelta", InputActionType.Value, "<Pointer>/delta");
            _pointerDelta.Enable();

            // Mouse scroll is Vector2; y is wheel
            _scroll = new InputAction("Scroll", InputActionType.Value, "<Mouse>/scroll");
            _scroll.Enable();
        }

        private void OnDisable()
        {
            _panButton?.Disable();
            _pointerDelta?.Disable();
            _scroll?.Disable();

            _panButton?.Dispose();
            _pointerDelta?.Dispose();
            _scroll?.Dispose();

            _panButton = null;
            _pointerDelta = null;
            _scroll = null;
        }

        private void Update()
        {
            // Zoom
            Vector2 scroll = _scroll.ReadValue<Vector2>();
            if (scroll.y != 0f)
            {
                // scroll.y tipicamente è ±120 per notch su mouse classici
                float zoomDelta = -scroll.y * zoomSpeed * Time.unscaledDeltaTime;
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize + zoomDelta, minOrthoSize, maxOrthoSize);
            }

            // Pan (RMB + mouse move)
            if (_panButton.IsPressed())
            {
                Vector2 delta = _pointerDelta.ReadValue<Vector2>();

                // Pan più ampio quando sei zoom-out
                float zoomScale = 1f + (_cam.orthographicSize * panSpeedZoomFactor);

                // Convertiamo delta pixel in movimento mondo (asse Y invertito a gusto; qui segue il trascinamento)
                Vector3 move = new Vector3(-delta.x, -delta.y, 0f) * (panSpeed * zoomScale);
                transform.position += move;
            }
        }
    }
}
