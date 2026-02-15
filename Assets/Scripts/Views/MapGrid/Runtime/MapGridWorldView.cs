using System.Collections.Generic;
using UnityEngine;
using Arcontio.Core;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// View binder: sincronizza World -> SpriteRenderers (NPC + Objects).
    /// Non scrive nel core: solo lettura.
    /// </summary>
    public sealed class MapGridWorldView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MapGridConfig cfg;

        [Header("Sprite fallbacks")]
        [SerializeField] private string defaultNpcSpritePath = "MapGrid/Sprites/NPC_Astro";

        [Header("Sorting")]
        [SerializeField] private int terrainOrder = 0;
        [SerializeField] private int objectBaseOrder = 50;
        [SerializeField] private int npcBaseOrder = 100;
        [SerializeField] private bool sortByY = true; // DF-like

        private World _world;

        private Transform _objectsRoot;
        private Transform _npcsRoot;

        private readonly Dictionary<int, SpriteRenderer> _npcViews = new();
        private readonly Dictionary<int, SpriteRenderer> _objectViews = new();
        private readonly Dictionary<string, Sprite> _spriteCache = new();

        private Sprite _defaultNpcSprite;

        public void Init(MapGridConfig config)
        {
            cfg = config;

            _objectsRoot = new GameObject("ObjectViews").transform;
            _objectsRoot.SetParent(transform, false);

            _npcsRoot = new GameObject("NPCViews").transform;
            _npcsRoot.SetParent(transform, false);

            // default npc sprite: prima da config, altrimenti fallback
            var npcPath = cfg?.npc?.spriteResourcePath;
            if (string.IsNullOrWhiteSpace(npcPath)) npcPath = defaultNpcSpritePath;
            _defaultNpcSprite = LoadSpriteCached(npcPath);

            if (_defaultNpcSprite == null)
                Debug.LogWarning($"[MapGrid] Default NPC sprite not found at Resources/{npcPath}.png");
        }

        private void Update()
        {
            if (cfg == null) return;

            // Bind al world (ritenta finché non c'è)
            _world ??= MapGridWorldProvider.TryGetWorld();
            if (_world == null) return;

            SyncObjects();
            SyncNpcs();

            // (opzionale) cleanup: se entità spariscono, rimuovi view
            CleanupMissing();
        }

        private void SyncNpcs()
        {
            foreach (var kv in _world.GridPos)
            {
                int npcId = kv.Key;
                var pos = kv.Value;

                if (!_npcViews.TryGetValue(npcId, out var sr) || sr == null)
                {
                    sr = CreateSpriteRenderer(_npcsRoot, $"NPC_{npcId}", _defaultNpcSprite);
                    _npcViews[npcId] = sr;
                }

                sr.transform.position = CellCenterWorld(pos.X, pos.Y);

                sr.sortingOrder = sortByY
                    ? npcBaseOrder - pos.Y
                    : npcBaseOrder;
            }
        }

        private void SyncObjects()
        {
            foreach (var kv in _world.Objects)
            {
                int objId = kv.Key;
                var inst = kv.Value;

                if (!_objectViews.TryGetValue(objId, out var sr) || sr == null)
                {
                    // risolvi spriteKey da ObjectDef (se presente), altrimenti fallback
                    string spriteKey = null;

                    if (_world.TryGetObjectDef(inst.DefId, out var def))
                    {
                        // Il tuo JSON ha "SpriteKey". Assumo che in C# ObjectDef abbia "SpriteKey".
                        spriteKey = def.SpriteKey;
                    }

                    if (string.IsNullOrWhiteSpace(spriteKey))
                        spriteKey = $"MapGrid/Sprites/Objects/{inst.DefId}";

                    var sprite = LoadSpriteCached(spriteKey);
                    if (sprite == null)
                        Debug.LogWarning($"[MapGrid] Missing object sprite for defId='{inst.DefId}' at Resources/{spriteKey}.png");

                    sr = CreateSpriteRenderer(_objectsRoot, $"OBJ_{objId}_{inst.DefId}", sprite);
                    _objectViews[objId] = sr;
                }

                sr.transform.position = CellCenterWorld(inst.CellX, inst.CellY);

                sr.sortingOrder = sortByY
                    ? objectBaseOrder - inst.CellY
                    : objectBaseOrder;

                // Stock label (solo se è FoodStock)
                var label = sr.GetComponent<MapGridStockLabel>();
                if (label == null) label = sr.gameObject.AddComponent<MapGridStockLabel>();

                if (_world.FoodStocks.TryGetValue(objId, out var stock))
                {
                    label.SetText(stock.Units.ToString());
                    label.SetSorting(sr.sortingOrder);
                }
                else
                {
                    label.SetText("");
                }
            }
        }

        private void CleanupMissing()
        {
            // NPC: se non esiste più in GridPos, distruggi view
            // (Oggi è ok O(n) perché poche entità; se cresce, ottimizziamo con stamp tick.)
            var npcToRemove = ListPool<int>.Get();
            foreach (var id in _npcViews.Keys)
                if (!_world.GridPos.ContainsKey(id))
                    npcToRemove.Add(id);

            foreach (var id in npcToRemove)
            {
                if (_npcViews.TryGetValue(id, out var sr) && sr != null)
                    Destroy(sr.gameObject);
                _npcViews.Remove(id);
            }
            ListPool<int>.Release(npcToRemove);

            var objToRemove = ListPool<int>.Get();
            foreach (var id in _objectViews.Keys)
                if (!_world.Objects.ContainsKey(id))
                    objToRemove.Add(id);

            foreach (var id in objToRemove)
            {
                if (_objectViews.TryGetValue(id, out var sr) && sr != null)
                    Destroy(sr.gameObject);
                _objectViews.Remove(id);
            }
            ListPool<int>.Release(objToRemove);
        }

        private Vector3 CellCenterWorld(int cellX, int cellY)
        {
            float wx = (cellX + 0.5f) * cfg.tileSizeWorld;
            float wy = (cellY + 0.5f) * cfg.tileSizeWorld;
            return new Vector3(wx, wy, 0f);
        }

        private SpriteRenderer CreateSpriteRenderer(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = 0;

            // Se vuoi evitare che terreno copra sprites: assicurati terrenoOrder << objectBaseOrder
            // Il terreno (mesh) non usa sortingOrder; quindi questo va bene.

            // Se sprite null, l'oggetto esiste comunque e lo vedi in hierarchy (debug).
            return sr;
        }

        private Sprite LoadSpriteCached(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return null;

            if (_spriteCache.TryGetValue(resourcePath, out var s) && s != null)
                return s;

            s = Resources.Load<Sprite>(resourcePath);
            _spriteCache[resourcePath] = s;
            return s;
        }

        /// <summary>
        /// Piccola pool per evitare allocazioni in cleanup (debug-friendly).
        /// </summary>
        private static class ListPool<T>
        {
            private static readonly Stack<List<T>> _pool = new();
            public static List<T> Get() => _pool.Count > 0 ? _pool.Pop() : new List<T>(64);
            public static void Release(List<T> list) { list.Clear(); _pool.Push(list); }
        }
    }
}
