using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// Gestisce la conversione tileId -> UV rect sull'atlas.
    ///
    /// Assunzioni (per partire veloce):
    /// - Atlas è una Texture2D
    /// - È una griglia regolare di tile (32x32)
    /// - TileDef fornisce (uvX, uvY) in coordinate di griglia:
    ///   uvX crescente verso destra
    ///   uvY crescente verso il basso O verso l'alto (qui fissiamo convenzione)
    ///
    /// Convenzione adottata qui:
    /// - uvY=0 indica la prima riga in ALTO dell'immagine (stile sprite-sheet).
    /// - Unity UV però usa origine in basso: facciamo conversione.
    ///
    /// Se un giorno cambierai convenzione, modifichi SOLO qui.
    /// </summary>
    public sealed class MapGridTileAtlas
    {
        public Texture2D Texture { get; }
        public int TilePixels { get; }

        public int TilesPerRow { get; }
        public int TilesPerCol { get; }

        private readonly Dictionary<int, Vector2Int> _tileUv = new();

        public MapGridTileAtlas(Texture2D tex, int tilePixels)
        {
            Texture = tex;
            TilePixels = tilePixels;

            TilesPerRow = tex.width / tilePixels;
            TilesPerCol = tex.height / tilePixels;
        }

        public void Register(int tileId, int uvX, int uvY)
        {
            _tileUv[tileId] = new Vector2Int(uvX, uvY);
        }

        /// <summary>
        /// Calcola le 4 UV per un quad (tile) dato tileId.
        /// Restituisce:
        /// - uv0 bottom-left
        /// - uv1 bottom-right
        /// - uv2 top-right
        /// - uv3 top-left
        /// </summary>
        public void GetUvQuad(int tileId, out Vector2 uv0, out Vector2 uv1, out Vector2 uv2, out Vector2 uv3)
        {
            if (!_tileUv.TryGetValue(tileId, out var cell))
                cell = Vector2Int.zero;

            float invW = 1f / TilesPerRow;
            float invH = 1f / TilesPerCol;

            // Convertiamo uvY da "top origin" (sprite sheet) a "bottom origin" (UV standard)
            int yFromBottom = (TilesPerCol - 1) - cell.y;

            float uMin = cell.x * invW;
            float vMin = yFromBottom * invH;
            float uMax = uMin + invW;
            float vMax = vMin + invH;

            uv0 = new Vector2(uMin, vMin);
            uv1 = new Vector2(uMax, vMin);
            uv2 = new Vector2(uMax, vMax);
            uv3 = new Vector2(uMin, vMax);
        }
    }
}
