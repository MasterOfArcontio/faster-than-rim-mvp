using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Arcontio.View
{
    /// <summary>
    /// Overlay per mostrare log rich-text (Unity color tags).
    /// - Static queue: il sink può chiamare Enqueue() anche se l'overlay non esiste ancora.
    /// - Ring buffer: ultime N righe.
    /// </summary>
    public sealed class ArcontioLogOverlay : MonoBehaviour
    {
        // --------- Static ingress (sink -> overlay) ----------
        private static readonly Queue<string> _pending = new();
        private static ArcontioLogOverlay _instance;

        public static void Enqueue(string richLine)
        {
            // Accumula anche se overlay non è ancora in scena
            lock (_pending)
            {
                _pending.Enqueue(richLine);
                // limitiamo memoria in caso overlay non creato
                while (_pending.Count > 500) _pending.Dequeue();
            }

            // se esiste, push immediato
            _instance?.DrainPending();
        }

        // --------- Instance ----------
        [Header("Placement")]
        [SerializeField] private bool bottomRight = true;

        [Header("Buffer")]
        [SerializeField] private int maxLines = 45;

        [Header("Size")]
        [SerializeField] private float width = 560f;
        [SerializeField] private float height = 280f;

        [Header("Text")]
        [SerializeField] private int fontSize = 12;

        private readonly Queue<string> _lines = new();
        private Text _text;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUi();
            DrainPending();
        }

        private void Update()
        {
            // drain anche se arrivano linee dopo Awake
            DrainPending();
        }

        private void BuildUi()
        {
            // Canvas root
            var canvasGo = new GameObject("ArcontioLogCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGo.AddComponent<GraphicRaycaster>();

            // Panel
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);

            var img = panelGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);

            var rt = panelGo.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(width, height);

            if (bottomRight)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-12f, 12f);
            }
            else
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-12f, -12f);
            }

            // Text
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(panelGo.transform, false);

            _text = textGo.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = fontSize;
            _text.alignment = TextAnchor.UpperLeft;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Truncate;
            _text.supportRichText = true;
            _text.color = Color.white;
            _text.text = "LOG OVERLAY READY";
            _text.enabled = true;

            var trt = textGo.GetComponent<RectTransform>();
            trt.localScale = Vector3.one;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(10f, 10f);
            trt.offsetMax = new Vector2(-10f, -10f);
        }

        private void DrainPending()
        {
            if (_text == null) return;

            bool changed = false;

            lock (_pending)
            {
                while (_pending.Count > 0)
                {
                    var line = _pending.Dequeue();
                    _lines.Enqueue(line);
                    while (_lines.Count > maxLines) _lines.Dequeue();
                    changed = true;
                }
            }

            if (changed)
                _text.text = string.Join("\n", _lines);
        }
    }
}
