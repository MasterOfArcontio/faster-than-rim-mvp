using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Views.MapGrid
{
    public sealed class MapGridView : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private int width = 30;
        [SerializeField] private int height = 20;
        [SerializeField] private float cellSize = 1f;

        [Header("Rendering")]
        [SerializeField] private bool drawGrid = true;
        [SerializeField] private bool drawNpcMarkers = true;
        [SerializeField] private float npcMarkerScale = 0.6f;

        private Transform _gridRoot;
        private Transform _npcRoot;

        private Sprite _whiteSprite;

        // npcId -> marker GO
        private readonly Dictionary<int, GameObject> _markers = new();

        private void Awake()
        {
            _gridRoot = new GameObject("GridRoot").transform;
            _gridRoot.SetParent(transform, worldPositionStays: false);

            _npcRoot = new GameObject("NpcRoot").transform;
            _npcRoot.SetParent(transform, worldPositionStays: false);

            _whiteSprite = CreateWhiteSprite();
        }

        private void Start()
        {
            if (drawGrid)
                BuildGrid();

            // Prima sync marker
            if (drawNpcMarkers)
                SyncAllNpcMarkers();
        }

        private void Update()
        {
            if (!drawNpcMarkers) return;

            // Aggiornamento semplice: ogni frame riallinea marker esistenti e crea quelli mancanti.
            // Per ora va bene per debug. Poi lo ottimizziamo (eventi/dirty set).
            SyncAllNpcMarkers();
        }

        private void BuildGrid()
        {
            // pulizia eventuale
            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
                Destroy(_gridRoot.GetChild(i).gameObject);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = new GameObject($"Tile_{x}_{y}");
                    tile.transform.SetParent(_gridRoot, worldPositionStays: false);

                    tile.transform.localPosition = CellToWorld(x, y);
                    tile.transform.localScale = new Vector3(cellSize, cellSize, 1f);

                    var sr = tile.AddComponent<SpriteRenderer>();
                    sr.sprite = _whiteSprite;

                    // Alternanza scacchiera (colori volutamente neutri)
                    bool dark = ((x + y) & 1) == 0;
                    sr.color = dark ? new Color(0.22f, 0.22f, 0.22f, 1f) : new Color(0.28f, 0.28f, 0.28f, 1f);

                    // Ordine dietro agli NPC
                    sr.sortingOrder = 0;
                }
            }
        }

        private void SyncAllNpcMarkers()
        {
            var host = Arcontio.Core.SimulationHost.Instance;
            if (host == null) return;

            var world = host.World;
            if (world == null) return;

            // 1) crea/aggiorna marker per ogni NPC che ha GridPos
            foreach (var kv in world.GridPos)
            {
                int npcId = kv.Key;
                var pos = kv.Value;

                if (!_markers.TryGetValue(npcId, out var go) || go == null)
                {
                    go = CreateNpcMarker(npcId);
                    _markers[npcId] = go;
                }

                go.transform.localPosition = CellToWorld(pos.X, pos.Y);
            }

            // 2) rimuovi marker di NPC non più presenti in GridPos
            // (copia keys per sicurezza)
            _tmpKeys.Clear();
            foreach (var id in _markers.Keys) _tmpKeys.Add(id);

            for (int i = 0; i < _tmpKeys.Count; i++)
            {
                int id = _tmpKeys[i];
                if (!world.GridPos.ContainsKey(id))
                {
                    if (_markers.TryGetValue(id, out var go) && go != null)
                        Destroy(go);

                    _markers.Remove(id);
                }
            }
        }

        private readonly List<int> _tmpKeys = new(2048);

        private GameObject CreateNpcMarker(int npcId)
        {
            var go = new GameObject($"NPC_{npcId}");
            go.transform.SetParent(_npcRoot, worldPositionStays: false);

            go.transform.localScale = new Vector3(cellSize * npcMarkerScale, cellSize * npcMarkerScale, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _whiteSprite;
            sr.color = new Color(0.90f, 0.90f, 0.20f, 1f);
            sr.sortingOrder = 10;

            return go;
        }

        private Vector3 CellToWorld(int x, int y)
        {
            // Centro cella
            return new Vector3((x + 0.5f) * cellSize, (y + 0.5f) * cellSize, 0f);
        }

        private static Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            // pixelsPerUnit = 1 così la scala è diretta
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), pixelsPerUnit: 1);
        }
    }
}
