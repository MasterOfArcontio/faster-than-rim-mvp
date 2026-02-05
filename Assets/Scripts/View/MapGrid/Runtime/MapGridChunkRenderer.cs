using UnityEngine;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// Render terreno "a chunk" tramite mesh.
    ///
    /// Perché chunked?
    /// - Se la mappa è 200x200 -> 40.000 celle.
    /// - Un GameObject per tile è ingestibile.
    /// - Con chunk 16x16, hai mesh da 256 tile ciascuna:
    ///   - 200/16 ~ 13 chunk per lato -> ~169 chunk totali
    ///   - molto più efficiente e ragionevole
    ///
    /// Questo renderer disegna SOLO il TerrainLayer (layer 0).
    /// Strutture/overlay/NPC sono layer separati.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MapGridChunkRenderer : MonoBehaviour
    {
        private Mesh _mesh;

        /// <summary>
        /// Costruisce la mesh del chunk (cx,cy) usando i tileId presenti in MapGridData.
        ///
        /// Parametri:
        /// - map: dati cella -> tileId
        /// - atlas: conversione tileId -> UV
        /// - chunkX/chunkY: coordinate chunk
        /// - chunkSize: dimensione chunk (es. 16)
        /// - tileWorld: dimensione tile in world units (es. 1)
        /// </summary>
        public void Build(
            MapGridData map,
            MapGridTileAtlas atlas,
            int chunkX,
            int chunkY,
            int chunkSize,
            float tileWorld)
        {
            // Creiamo una mesh solo la prima volta e la riutilizziamo (più efficiente).
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

            // Ogni cella = un quad:
            // - 4 vertici
            // - 6 indici triangoli (2 triangoli)
            var verts = new Vector3[cellCount * 4];
            var uvs = new Vector2[cellCount * 4];
            var tris = new int[cellCount * 6];

            int v = 0;
            int t = 0;

            // Nota: coordinate world sul piano XY.
            // Se vuoi pseudo-isometrico stile RimWorld, lo ottieni con camera/transform,
            // non cambiando la logica della griglia.
            for (int y = startY; y < maxY; y++)
                for (int x = startX; x < maxX; x++)
                {
                    int tileId = map.GetTerrain(x, y);

                    atlas.GetUvQuad(tileId, out var uv0, out var uv1, out var uv2, out var uv3);

                    float wx = x * tileWorld;
                    float wy = y * tileWorld;

                    // Quad: bottom-left, bottom-right, top-right, top-left
                    verts[v + 0] = new Vector3(wx, wy, 0);
                    verts[v + 1] = new Vector3(wx + tileWorld, wy, 0);
                    verts[v + 2] = new Vector3(wx + tileWorld, wy + tileWorld, 0);
                    verts[v + 3] = new Vector3(wx, wy + tileWorld, 0);

                    uvs[v + 0] = uv0;
                    uvs[v + 1] = uv1;
                    uvs[v + 2] = uv2;
                    uvs[v + 3] = uv3;

                    // Triangoli (winding order standard)
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

            // Bounds necessari per culling corretto.
            _mesh.RecalculateBounds();
        }
    }
}
