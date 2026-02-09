using System.Collections;
using UnityEngine;
using TMPro;

namespace SocialViewer.UI.Overlay
{
    /// <summary>
    /// Controller tooltip:
    /// - Mostra testo con ritardo
    /// - Fade-in morbido
    /// - Posizionamento a partire da una posizione schermo (screen space)
    /// - Offset verso "alto-sinistra" rispetto al punto di ancoraggio passato
    /// 
    /// Nota: richiede un CanvasGroup sul TooltipPanel.
    /// </summary>
    public class TooltipController : MonoBehaviour
    {
        [Header("Riferimenti UI")]
        [SerializeField] private RectTransform tooltipPanel;
        [SerializeField] private TMP_Text tooltipText;
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Comportamento")]
        [Tooltip("Ritardo prima di mostrare il tooltip (secondi).")]
        [SerializeField] private float showDelay = 0.12f;

        [Tooltip("Durata del fade-in (secondi).")]
        [SerializeField] private float fadeInTime = 0.10f;

        [Header("Offset (pixel) rispetto al punto passato")]
        [Tooltip("Offset applicato al punto schermo passato. Valori tipici: x negativo, y positivo per alto-sinistra.")]
        [SerializeField] private Vector2 offsetTopLeft = new Vector2(-14f, 14f);

        private Coroutine _routine;
        private bool _requestedVisible;
        private Vector2 _requestedScreenPos;

        private void Awake()
        {
            // Recuperi automatici se non assegnati
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvasGroup == null && tooltipPanel != null) canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();

            HideImmediate();
        }

        /// <summary>
        /// Mostra il tooltip (con delay + fade) usando una posizione schermo come ancoraggio.
        /// </summary>
        public void ShowAt(Vector2 screenPos, string text)
        {
            //Debug.Log($"[Tooltip] ShowAt CALLED textlen={text?.Length ?? 0} screenPos={screenPos}");
            //Debug.Log($"[Tooltip] refs panel={(tooltipPanel!=null)} text={(tooltipText != null)} canvas={(canvas != null)} group={(canvasGroup != null)}");

            if (tooltipPanel == null || tooltipText == null || canvas == null || canvasGroup == null)
            {
                Debug.LogWarning("[TooltipController] Riferimenti mancanti (tooltipPanel/tooltipText/canvas/canvasGroup).");
                return;
            }

            _requestedVisible = true;
            _requestedScreenPos = screenPos;

            tooltipText.text = text;

            UnityEngine.Canvas.ForceUpdateCanvases();
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipPanel);

            // Riavvia la routine: evita flicker se cambi target rapidamente
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine());
        }

        /// <summary>
        /// Nasconde il tooltip.
        /// </summary>
        public void Hide()
        {
            _requestedVisible = false;

            if (_routine != null) StopCoroutine(_routine);
            _routine = null;

            HideImmediate();
        }

        private IEnumerator ShowRoutine()
        {
            // Delay
            if (showDelay > 0f)
                yield return new WaitForSecondsRealtime(showDelay);

            if (!_requestedVisible) yield break;

            tooltipPanel.gameObject.SetActive(true);
            canvasGroup.alpha = 0f;

            // Posiziona prima del fade
            SetPosition(_requestedScreenPos);

            // Fade-in
            float t = 0f;
            float dur = Mathf.Max(0.001f, fadeInTime);

            while (t < dur)
            {
                if (!_requestedVisible) yield break;

                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(t / dur);

                yield return null;
            }

            canvasGroup.alpha = 1f;
        }

        private void SetPosition(Vector2 screenPos)
        {
            // Applica offset alto-sinistra
            Vector2 sp = screenPos + offsetTopLeft;

            RectTransform canvasRect = (RectTransform)canvas.transform;

            Camera cam = null;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                sp,
                cam,
                out Vector2 localPoint
            );

            tooltipPanel.anchoredPosition = localPoint;
        }

        private void HideImmediate()
        {
            if (tooltipPanel != null) tooltipPanel.gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }
    }
}
