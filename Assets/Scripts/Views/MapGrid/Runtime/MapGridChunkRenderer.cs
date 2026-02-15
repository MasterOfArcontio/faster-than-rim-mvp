using UnityEngine;

namespace Arcontio.View.MapGrid
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MapGridChunkRenderer : MonoBehaviour
    {
        private Mesh _mesh;

        /// <summary>
        /// Regole visive minime “DF Steam-like”.
        /// - floorBaseTileId: tile base del pavimento (le varianti saranno floorBaseTileId + [0..floorVariantCount-1])
        /// - floorVariantCount: quante varianti contigue (es. 4)
        /// - wallTileId: tile muro pieno
        /// - wallTopTileId: tile "muro con top" (quando sopra c'è floor)
        /// </summary>
        public void Build(
            MapGridData map,
            MapGridTileAtlas atlas,
            int chunkX,
            int chunkY,
            int chunkSize,
            float tileWorld,
            int floorBaseTileId,
            int floorVariantCount,
            int wallTileId,
            int wallTopTileId)
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = $"MapGridChunk_{chunkX}_{chunkY}" };
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
            else
            {
                _mesh.Clear();
            }

            int startX = chunkX * chunkSize;
            int startY = chunkY * chunkSize;

            int maxX = Mathf.Min(startX + chunkSize, map.Width);
            int maxY = Mathf.Min(startY + chunkSize, map.Height);

            int cellsW = maxX - startX;
            int cellsH = maxY - startY;
            int cellCount = cellsW * cellsH;

            var verts = new Vector3[cellCount * 4];
            var uvs = new Vector2[cellCount * 4];
            var tris = new int[cellCount * 6];

            int v = 0;
            int t = 0;

            for (int y = startY; y < maxY; y++)
                for (int x = startX; x < maxX; x++)
                {
                    int tileId = ResolveVisualTileId(map, x, y, floorBaseTileId, floorVariantCount, wallTileId, wallTopTileId);

                    atlas.GetUvQuad(tileId, out var uv0, out var uv1, out var uv2, out var uv3);

                    float wx = x * tileWorld;
                    float wy = y * tileWorld;

                    verts[v + 0] = new Vector3(wx, wy, 0);
                    verts[v + 1] = new Vector3(wx + tileWorld, wy, 0);
                    verts[v + 2] = new Vector3(wx + tileWorld, wy + tileWorld, 0);
                    verts[v + 3] = new Vector3(wx, wy + tileWorld, 0);

                    uvs[v + 0] = uv0;
                    uvs[v + 1] = uv1;
                    uvs[v + 2] = uv2;
                    uvs[v + 3] = uv3;

                    tris[t + 0] = v + 0;
                    tris[t + 1] = v + 2;
                    tris[t + 2] = v + 1;

                    tris[t + 3] = v + 0;
                    tris[t + 4] = v + 3;
                    tris[t + 5] = v + 2;

                    v += 4;
                    t += 6;
                }

            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.triangles = tris;
            _mesh.RecalculateBounds();
        }

        private static int ResolveVisualTileId(
            MapGridData map,
            int x,
            int y,
            int floorBaseTileId,
            int floorVariantCount,
            int wallTileId,
            int wallTopTileId)
        {
            // 1) Se è bloccata -> muro (con top se sopra è floor).
            // Assumo che MapGridData esponga IsBlocked(x,y) come nel tuo Bootstrap.
            if (map.IsBlocked(x, y))
            {
                bool hasNorth = map.InBounds(x, y + 1);
                bool northIsFloor = hasNorth && !map.IsBlocked(x, y + 1);

                return northIsFloor ? wallTopTileId : wallTileId;
            }

            // 2) Floor variant deterministica (DF-style “vivo ma stabile”)
            // Nota: non uso Random, uso hash (x,y).
            if (floorVariantCount <= 1)
                return floorBaseTileId;

            int h = Hash2D(x, y);
            int k = Mathf.Abs(h) % floorVariantCount;
            return floorBaseTileId + k;
        }

        private static int Hash2D(int x, int y)
        {
            unchecked
            {
                // hash integer semplice ma stabile
                int h = 17;
                h = h * 31 + x;
                h = h * 31 + y;
                h ^= (h << 13);
                h ^= (h >> 17);
                h ^= (h << 5);
                return h;
            }
        }
    }
}
