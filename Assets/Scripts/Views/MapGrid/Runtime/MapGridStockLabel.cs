using UnityEngine;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// Label world-space minimale sopra uno sprite (quantità stock, ecc.).
    /// Usa TextMesh (no Canvas).
    /// </summary>
    public sealed class MapGridStockLabel : MonoBehaviour
    {
        [SerializeField] private int orderOffset = 1;
        private TextMesh _tm;
        private MeshRenderer _mr;

        public void EnsureCreated()
        {
            if (_tm != null) return;

            var go = new GameObject("StockLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.28f, 0f); // sopra lo sprite (tune)

            _tm = go.AddComponent<TextMesh>();
            _tm.anchor = TextAnchor.MiddleCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.characterSize = 0.1f;     // tune
            _tm.fontSize = 64;            // tune
            _tm.richText = true;
            _tm.text = "";

            _mr = go.GetComponent<MeshRenderer>();
        }

        public void SetText(string text)
        {
            EnsureCreated();
            _tm.text = text;
            _mr.enabled = !string.IsNullOrEmpty(text);
        }

        public void SetSorting(int baseOrder)
        {
            EnsureCreated();
            if (_mr != null)
                _mr.sortingOrder = baseOrder + orderOffset;
        }
    }
}
