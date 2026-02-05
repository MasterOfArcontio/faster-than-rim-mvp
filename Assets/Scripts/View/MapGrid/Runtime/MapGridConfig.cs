using System;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// DTO (Data Transfer Object) serializzabile via JsonUtility.
    ///
    /// Contiene la configurazione "ufficiale" della scena MapGrid:
    /// - dimensioni mappa
    /// - parametri atlas (tile size, definizioni UV)
    /// - parametri chunking (dimensione chunk)
    /// - parametri camera (zoom, edge-pan)
    /// - riferimenti a risorse (Resources path)
    ///
    /// NOTE IMPORTANTI:
    /// 1) JsonUtility supporta bene solo campi pubblici e classi [Serializable].
    /// 2) Le path sono relative a "Assets/Resources" senza estensione.
    /// </summary>
    [Serializable]
    public sealed class MapGridConfig
    {
        // Dimensioni logiche della mappa (numero di celle in X e Y).
        public int mapWidth;
        public int mapHeight;

        // Dimensione della singola cella in world units (es. 1 unità).
        public float tileSizeWorld = 1f;

        // Dimensione in pixel di una tile nell'atlas (es. 32x32).
        public int tilePixels = 32;

        // Chunk size (es. 16) -> la mappa viene divisa in blocchi 16x16 per render efficiente.
        public int chunkSize = 16;

        // Path in Resources per la Texture2D dell'atlas terreno.
        public string terrainAtlasResourcePath;

        // Definizioni tile: associano tileId -> coordinate (uvX,uvY) nell'atlas a griglia.
        public TileDef[] tileDefs;

        // Path in Resources del layout (dove sono tile, ostacoli, risorse).
        public string layoutResourcePath;

        // Parametri camera.
        public CameraConfig camera;

        // Parametri NPC (per visualizzazione provvisoria / bootstrap).
        public NpcConfig npc;

        // -----------------------------
        // NUOVI PARAMETRI PAN (inerzia)
        // -----------------------------

        // Quanto "morbido" è il pan (SmoothDamp time).
        // Valori tipici: 0.05 (molto reattivo) -> 0.20 (molto fluido).
        public float panSmoothTime = 0.10f;

        // Limite alla velocità massima del pan, per evitare scatti enormi (world units/sec).
        public float panMaxSpeed = 200f;

        // Abilita/disabilita RMB drag pan (di default: attivo).
        public bool rightMouseDragPan = true;

        [Serializable]
        public sealed class TileDef
        {
            // ID logico del tipo di tile (usato nella mappa).
            public int id;

            // Nome umano (solo debug/documentazione).
            public string name;

            // Coordinate della tile nell'atlas a griglia (0,0) = prima cella in alto a sinistra (convenz.).
            public int uvX;
            public int uvY;
        }

        [Serializable]
        public sealed class CameraConfig
        {
            // Zoom iniziale (orthographicSize).
            public float startZoom = 18f;

            // Limiti zoom.
            public float minZoom = 6f;
            public float maxZoom = 40f;

            // Velocità zoom (quanto cambia orthographicSize per scatto rotellina).
            public float zoomSpeed = 2f;

            // Edge-pan: quanti pixel dal bordo schermo consideriamo "zona pan".
            public int edgePanBorderPx = 18;

            // Edge-pan: velocità in world units/sec.
            public float edgePanSpeedWorld = 18f;

            // -----------------------------
            // NUOVI PARAMETRI PAN (inerzia)
            // -----------------------------

            // Quanto "morbido" è il pan (SmoothDamp time).
            // Valori tipici: 0.05 (molto reattivo) -> 0.20 (molto fluido).
            public float panSmoothTime = 0.10f;

            // Limite alla velocità massima del pan, per evitare scatti enormi (world units/sec).
            public float panMaxSpeed = 200f;

            // Abilita/disabilita RMB drag pan (di default: attivo).
            public bool rightMouseDragPan = true;

        }

        [Serializable]
        public sealed class NpcConfig
        {
            // Path in Resources di uno Sprite per NPC.
            // (Per ora: sprite singolo; poi potrai sostituire con sprite sheet / animazioni.)
            public string spriteResourcePath;

            // Quanti NPC di test spawnare in assenza di binding col simulatore.
            public int spawnCount = 10;
        }
    }


    [Serializable]
    public sealed class CameraConfig
    {
        // Zoom iniziale (orthographicSize).
        public float startZoom = 18f;

        // Limiti zoom.
        public float minZoom = 6f;
        public float maxZoom = 40f;

        // Velocità zoom (quanto cambia orthographicSize per scatto rotellina).
        public float zoomSpeed = 2f;

        // Edge-pan: quanti pixel dal bordo schermo consideriamo "zona pan".
        public int edgePanBorderPx = 18;

        // Edge-pan: velocità in world units/sec.
        public float edgePanSpeedWorld = 18f;

        // -----------------------------
        // NUOVI PARAMETRI PAN (inerzia)
        // -----------------------------

        // Quanto "morbido" è il pan (SmoothDamp time).
        // Valori tipici: 0.05 (molto reattivo) -> 0.20 (molto fluido).
        public float panSmoothTime = 0.10f;

        // Limite alla velocità massima del pan, per evitare scatti enormi (world units/sec).
        public float panMaxSpeed = 200f;

        // Abilita/disabilita RMB drag pan (di default: attivo).
        public bool rightMouseDragPan = true;
    }
}
