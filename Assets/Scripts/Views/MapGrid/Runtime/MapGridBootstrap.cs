using UnityEngine;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// Bootstrap della scena MapGrid.
    ///
    /// Responsabilità (ordinata):
    /// 1) Caricare MapGridConfig dal JSON ufficiale
    /// 2) Caricare MapGridLayout (struttura mappa)
    /// 3) Costruire MapGridData (buffer runtime per rendering)
    /// 4) Caricare atlas terreno e registrare tileDefs -> UV
    /// 5) Creare i chunk terrain (mesh chunked)
    /// 6) Setup camera: posizione iniziale + controller (edge-pan + zoom)
    /// 7) Posizionare NPC di test (placeholder) in coordinate sensate
    ///
    /// NOTA:
    /// - Questo è View-only.
    /// - Il binding col simulatore arriverà dopo (NpcViewManager).
    /// - Non usiamo FindObjectOfType: tutto è esplicito.
    /// </summary>
    public sealed class MapGridBootstrap : MonoBehaviour
    {
        [Header("Scene references")]
        [Tooltip("Camera usata per la vista mondo (orthographic).")]
        [SerializeField] private Camera worldCamera;

        [Tooltip("Un empty object che fungerà da 'rig' per il pan (consigliato). Se null, muoviamo la camera.")]
        [SerializeField] private Transform cameraRig;

        [Header("Materials")]
        [Tooltip("Material Unlit/Texture per disegnare la mesh terreno (atlas).")]
        [SerializeField] private Material terrainMaterial;

        // Stato runtime
        private MapGridConfig _cfg;
        private MapGridLayout _layout;
        private MapGridData _map;
        private MapGridTileAtlas _atlas;

        private void Awake()
        {
            // 0) Pre-check: camera
            if (worldCamera == null)
                worldCamera = Camera.main;

            if (worldCamera == null)
            {
                Debug.LogError("[MapGrid] No camera found. Assign 'worldCamera' in inspector.");
                return;
            }

            if (terrainMaterial == null)
            {
                // Fallback robusto: creiamo un materiale runtime usando uno shader unlit.
                // Questo evita blocchi in fase di sviluppo e rende la scena autosufficiente.
                var shader = Shader.Find("Unlit/Texture");
                if (shader == null)
                {
                    Debug.LogError("[MapGrid] Missing terrainMaterial AND Shader.Find('Unlit/Texture') failed. Create/assign a material manually.");
                    return;
                }

                terrainMaterial = new Material(shader)
                {
                    name = "MAT_MapGrid_Terrain_Unlit_Runtime"
                };
            }


            // 1) Load config JSON (ufficiale)
            _cfg = MapGridJsonLoader.LoadFromResources<MapGridConfig>("MapGrid/Config/MapGridConfig");
            if (_cfg == null) return;

            // 2) Load layout JSON (struttura mappa)
            _layout = MapGridJsonLoader.LoadFromResources<MapGridLayout>(_cfg.layoutResourcePath);
            // _layout può anche essere null: gestiamo fallback.

            // 3) Create MapGridData (buffer runtime)
            _map = new MapGridData(_cfg.mapWidth, _cfg.mapHeight);

            // 4) Apply layout -> popola _map (terrain + blocked)
            ApplyLayoutToMap(_layout);

            // 5) Load atlas texture
            var tex = Resources.Load<Texture2D>(_cfg.terrainAtlasResourcePath);
            if (tex == null)
            {
                Debug.LogError($"[MapGrid] Missing TerrainAtlas Texture2D at Resources/{_cfg.terrainAtlasResourcePath}");
                return;
            }

            // 6) Build atlas mapping (tileId -> UV)
            _atlas = new MapGridTileAtlas(tex, _cfg.tilePixels);
            if (_cfg.tileDefs != null)
            {
                foreach (var td in _cfg.tileDefs)
                    _atlas.Register(td.id, td.uvX, td.uvY);
            }

            // 7) Build terrain chunks
            BuildTerrainChunks();

            // 8) Setup camera: posizione iniziale + controller
            SetupCamera();

            // 9) Spawn NPC placeholder (finché non bindiamo al simulatore)
            SpawnNpcPlaceholders();
        }

        private void ApplyLayoutToMap(MapGridLayout layout)
        {
            // Fallback: riempi tutto con tile 0 (iron_barren).
            int fill = 0;

            if (layout != null && layout.terrain != null)
                fill = layout.terrain.fill;

            for (int y = 0; y < _map.Height; y++)
                for (int x = 0; x < _map.Width; x++)
                    _map.SetTerrain(x, y, fill);

            // Patch rettangolari (es. aree rocciose)
            if (layout?.terrain?.patches != null)
            {
                foreach (var p in layout.terrain.patches)
                {
                    for (int yy = p.y; yy < p.y + p.h; yy++)
                        for (int xx = p.x; xx < p.x + p.w; xx++)
                        {
                            if (_map.InBounds(xx, yy))
                                _map.SetTerrain(xx, yy, p.tileId);
                        }
                }
            }

            // Occluders -> celle bloccate (per ora bool semplice)
            if (layout?.occluders != null)
            {
                foreach (var o in layout.occluders)
                {
                    if (_map.InBounds(o.x, o.y))
                        _map.SetBlocked(o.x, o.y, true);
                }
            }

            // Resources: per ora non le disegniamo in questo pacchetto base,
            // ma le abbiamo nel layout per evoluzione futura.
        }

        private void BuildTerrainChunks()
        {
            int chunkSize = Mathf.Max(4, _cfg.chunkSize);

            int chunksX = Mathf.CeilToInt(_map.Width / (float)chunkSize);
            int chunksY = Mathf.CeilToInt(_map.Height / (float)chunkSize);

            var root = new GameObject("TerrainChunks").transform;
            root.SetParent(transform, false);

            for (int cy = 0; cy < chunksY; cy++)
                for (int cx = 0; cx < chunksX; cx++)
                {
                    var go = new GameObject($"Chunk_{cx}_{cy}");
                    go.transform.SetParent(root, false);

                    // Renderer + material
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.material = terrainMaterial;          // istanza per renderer (safe)

                    // Agganciamo la texture atlas al material
                    mr.material.mainTexture = _atlas.Texture;

                    // Mesh chunk
                    var cr = go.AddComponent<MapGridChunkRenderer>();
                    cr.Build(_map, _atlas, cx, cy, chunkSize, _cfg.tileSizeWorld);
                }
        }

        private void SetupCamera()
        {
            // Posizioniamo la camera al centro della mappa.
            float mapW = _map.Width * _cfg.tileSizeWorld;
            float mapH = _map.Height * _cfg.tileSizeWorld;

            float centerX = mapW * 0.5f;
            float centerY = mapH * 0.5f;

            // Se abbiamo un cameraRig, muoviamo quello.
            // Altrimenti muoviamo direttamente la camera.
            Transform target = cameraRig != null ? cameraRig : worldCamera.transform;

            // Z: se muoviamo rig su XY, la camera deve stare a Z -10.
            // Se rig è un empty, la camera deve essere figlia e posizionata a (0,0,-10).
            if (cameraRig != null)
            {
                cameraRig.position = new Vector3(centerX, centerY, 0f);

                // FIX: la camera deve seguire il rig, quindi la rendiamo figlia del rig.
                worldCamera.transform.SetParent(cameraRig, worldPositionStays: false);

                // Posizione locale standard per camera 2D: guarda verso Z=0 da Z negativo.
                worldCamera.transform.localPosition = new Vector3(0f, 0f, -10f);
                worldCamera.transform.localRotation = Quaternion.identity;
            }
            else
            {
                worldCamera.transform.position = new Vector3(centerX, centerY, -10f);
            }

            // Scegliamo chi muovere col pan:
            // - se esiste cameraRig, muoviamo quello
            // - altrimenti muoviamo la camera
            Transform panTarget = cameraRig != null ? cameraRig : worldCamera.transform;

            // Controller camera: attaccato al panTarget (NON alla camera se c’è rig)
            var ctrl = panTarget.GetComponent<MapGridCameraController>();
            if (ctrl == null)
                ctrl = panTarget.gameObject.AddComponent<MapGridCameraController>();

            ctrl.Init(worldCamera, _map, _cfg);

            // Controller camera (edge-pan + zoom)
            //ctrl = target.gameObject.AddComponent<MapGridCameraController>();
            ctrl.Init(worldCamera, _map, _cfg);
        }

        private void SpawnNpcPlaceholders()
        {
            // In questa fase:
            // - gli NPC reali sono creati dal simulatore,
            // - ma non abbiamo ancora scritto il binding.
            //
            // Quindi: spawn di test con coordinate "sensate":
            // - distribuzione casuale
            // - evita celle bloccate
            // - evita bordi (così non clippano subito)

            var sprite = Resources.Load<Sprite>(_cfg.npc.spriteResourcePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[MapGrid] NPC sprite missing at Resources/{_cfg.npc.spriteResourcePath} (OK for now).");
                return;
            }

            int count = Mathf.Max(1, _cfg.npc.spawnCount);
            var rng = new System.Random(123);

            var root = new GameObject("NPCViews").transform;
            root.SetParent(transform, false);

            for (int i = 0; i < count; i++)
            {
                // tentativi per trovare una cella libera
                for (int tries = 0; tries < 200; tries++)
                {
                    int x = rng.Next(2, _map.Width - 2);
                    int y = rng.Next(2, _map.Height - 2);

                    if (_map.IsBlocked(x, y))
                        continue;

                    CreateNpcView(root, sprite, x, y, i);
                    break;
                }
            }
        }

        private void CreateNpcView(Transform root, Sprite sprite, int cellX, int cellY, int index)
        {
            var go = new GameObject($"NPC_{index}_({cellX},{cellY})");
            go.transform.SetParent(root, false);

            // SpriteRenderer: scelta semplice e veloce per iniziare.
            // In futuro potrai sostituire con:
            // - animator 2D
            // - sprite sheet
            // - sorting per Y
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            // sortingOrder: i pawn devono stare sopra il terreno.
            // In futuro useremo sorting layer + sorting by Y.
            sr.sortingOrder = 100;

            // Posizione world: centro cella.
            float wx = (cellX + 0.5f) * _cfg.tileSizeWorld;
            float wy = (cellY + 0.5f) * _cfg.tileSizeWorld;

            go.transform.position = new Vector3(wx, wy, 0f);

            //
            // SCALING AUTOMATICO "TILE-FIT"
            //
            // Obiettivo: far sì che un NPC 32x32 occupi circa 1 tile world (tileSizeWorld=1).
            //
            // Unity usa Pixels Per Unit (PPU):
            // - worldWidthOfSprite = spriteWidthPx / PPU
            // Se PPU è 100 (default), uno sprite 32px è 0.32 world units -> sembra piccolo.
            // Qui scalamo in modo che la larghezza world dello sprite diventi tileSizeWorld.
            //
            float spriteWorldWidth = sprite.rect.width / sprite.pixelsPerUnit;
            if (spriteWorldWidth > 0.0001f)
            {
                float scale = _cfg.tileSizeWorld / spriteWorldWidth;
                go.transform.localScale = Vector3.one * scale;
            }
        }
    }
}
