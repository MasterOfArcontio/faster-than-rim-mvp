using UnityEngine;
using UnityEngine.InputSystem;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// Camera controller per MapGrid:
    ///
    /// - Pan con edge (mouse vicino al bordo)
    /// - Pan con drag RMB (tenendo premuto tasto destro)
    /// - Zoom con rotellina
    /// - "Zoom to cursor": la cella sotto il mouse resta sotto il mouse dopo lo zoom
    /// - Inerzia pan (SmoothDamp) per un feel meno rigido
    ///
    /// NOTE IMPORTANTI:
    /// - Per evitare "ballo" durante RMB drag, NON usiamo world-point ancorati alla camera
    ///   mentre essa si muove (feedback loop). Usiamo invece Mouse.delta in pixel e lo
    ///   convertiamo in world-units.
    /// </summary>
    public sealed class MapGridCameraController : MonoBehaviour
    {
        private Camera _camera;
        private MapGridData _map;
        private MapGridConfig _cfg;

        private float _tileWorld;

        // Target position (pan con inerzia verso questo punto)
        private Vector3 _targetPos;

        // Velocity usata internamente da SmoothDamp
        private Vector3 _panVelocity;

        // Stato drag RMB
        private bool _isRmbDragging;

        public void Init(Camera cam, MapGridData map, MapGridConfig cfg)
        {
            _camera = cam;
            _map = map;
            _cfg = cfg;
            _tileWorld = cfg.tileSizeWorld;

            _camera.orthographic = true;
            _camera.orthographicSize = cfg.camera.startZoom;

            _targetPos = transform.position;
        }

        private void Update()
        {
            if (_camera == null || _map == null || _cfg == null)
                return;

            HandleZoomToCursor();
            HandleRmbDragPanTarget();
            HandleEdgePanTarget(); // edge-pan viene ignorato mentre trascini RMB

            // Clamp target prima di applicare inerzia (così non inseguiamo target fuori mappa)
            _targetPos = ClampToMapBounds(_targetPos);

            // Applica inerzia verso target
            ApplyPanInertia();

            // Clamp di sicurezza anche sulla posizione reale
            transform.position = ClampToMapBounds(transform.position);
        }

        /// <summary>
        /// Zoom con rotellina + "zoom to cursor".
        /// Mantiene il world-point sotto il mouse invariato (quindi la stessa cella resta sotto il mouse).
        /// </summary>
        private void HandleZoomToCursor()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) < 0.01f) return;

            // 1) World point sotto mouse prima dello zoom
            Vector3 before = GetMouseWorldOnZ0();

            // 2) Applica zoom
            float sign = Mathf.Sign(scrollY);
            float delta = -sign * _cfg.camera.zoomSpeed;

            float newSize = Mathf.Clamp(
                _camera.orthographicSize + delta,
                _cfg.camera.minZoom,
                _cfg.camera.maxZoom);

            // Se non cambia, stop
            if (Mathf.Approximately(newSize, _camera.orthographicSize))
                return;

            _camera.orthographicSize = newSize;

            // 3) World point sotto mouse dopo lo zoom
            Vector3 after = GetMouseWorldOnZ0();

            // 4) Compensazione: sposta target in modo che "before" resti sotto il mouse
            Vector3 offset = before - after;
            _targetPos += offset;
        }

        /// <summary>
        /// RMB drag pan SENZA jitter:
        /// Usiamo Mouse.delta in pixel e lo convertiamo in world units.
        ///
        /// - Quando RMB premuto: entri in drag
        /// - RMB tenuto: aggiorni target usando delta del mouse
        /// - RMB rilasciato: esci (inerzia continua grazie a SmoothDamp)
        /// </summary>
        private void HandleRmbDragPanTarget()
        {
            // Se nel tuo MapGridConfig non hai ancora rightMouseDragPan, elimina questo if
            // oppure aggiungi il campo (come da patch precedente).
            if (_cfg.camera.rightMouseDragPan == false)
                return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _isRmbDragging = true;

                // Azzeriamo la velocity per evitare "slittamenti" strani quando inizi a trascinare.
                _panVelocity = Vector3.zero;
                return;
            }

            if (mouse.rightButton.wasReleasedThisFrame)
            {
                _isRmbDragging = false;
                return;
            }

            if (!_isRmbDragging || !mouse.rightButton.isPressed)
                return;

            // delta mouse in pixel (movimento dall'ultimo frame)
            Vector2 deltaPx = mouse.delta.ReadValue();

            if (deltaPx.sqrMagnitude < 0.0001f)
                return;

            // Conversione pixel -> world:
            // In ortografico, l'altezza visibile in world = 2*orthographicSize.
            // Quindi 1 pixel in Y vale (2*size)/Screen.height world units.
            float worldPerPixel = (2f * _camera.orthographicSize) / Screen.height;

            // Trascinamento "mappa": se muovi mouse a destra, vuoi trascinare il mondo a destra,
            // quindi la camera deve andare a sinistra => segno negativo.
            Vector3 deltaWorld = new Vector3(-deltaPx.x, -deltaPx.y, 0f) * worldPerPixel;

            _targetPos += deltaWorld;
        }

        /// <summary>
        /// Edge-pan: muove il target se il mouse è vicino ai bordi.
        /// Importante: se stai trascinando RMB, NON applichiamo edge-pan.
        /// </summary>
        private void HandleEdgePanTarget()
        {
            if (_isRmbDragging)
                return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();

            int border = _cfg.camera.edgePanBorderPx;
            float speed = _cfg.camera.edgePanSpeedWorld;

            Vector2 dir = Vector2.zero;

            if (pos.x <= border) dir.x = -1;
            else if (pos.x >= Screen.width - border) dir.x = 1;

            if (pos.y <= border) dir.y = -1;
            else if (pos.y >= Screen.height - border) dir.y = 1;

            if (dir == Vector2.zero) return;

            Vector3 move = new Vector3(dir.x, dir.y, 0f) * (speed * Time.deltaTime);
            _targetPos += move;
        }

        /// <summary>
        /// Inerzia pan verso target usando SmoothDamp.
        /// </summary>
        private void ApplyPanInertia()
        {
            float smoothTime = Mathf.Max(0.001f, _cfg.camera.panSmoothTime);
            float maxSpeed = Mathf.Max(0.01f, _cfg.camera.panMaxSpeed);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                _targetPos,
                ref _panVelocity,
                smoothTime,
                maxSpeed,
                Time.deltaTime);
        }

        /// <summary>
        /// Restituisce il world-point sotto il mouse sul piano Z=0.
        /// Usato SOLO per lo zoom-to-cursor (qui è stabile e non genera jitter).
        /// </summary>
        private Vector3 GetMouseWorldOnZ0()
        {
            var mouse = Mouse.current;
            if (mouse == null) return Vector3.zero;

            Vector2 mp = mouse.position.ReadValue();

            // distanza camera -> piano Z=0
            float zDist = Mathf.Abs(_camera.transform.position.z - 0f);

            Vector3 sp = new Vector3(mp.x, mp.y, zDist);
            Vector3 wp = _camera.ScreenToWorldPoint(sp);
            wp.z = 0f;
            return wp;
        }

        /// <summary>
        /// Clamp ai bounds mappa in world units.
        /// </summary>
        private Vector3 ClampToMapBounds(Vector3 pos)
        {
            float mapW = _map.Width * _tileWorld;
            float mapH = _map.Height * _tileWorld;

            float halfH = _camera.orthographicSize;
            float halfW = halfH * _camera.aspect;

            if (mapW <= halfW * 2f) pos.x = mapW * 0.5f;
            else pos.x = Mathf.Clamp(pos.x, halfW, mapW - halfW);

            if (mapH <= halfH * 2f) pos.y = mapH * 0.5f;
            else pos.y = Mathf.Clamp(pos.y, halfH, mapH - halfH);

            return pos;
        }
    }
}
