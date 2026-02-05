using System;
using System.Collections.Generic;
using UnityEngine;

namespace SocialViewer.UI.Graph
{
    /// <summary>
    /// SpatialHashGrid2D
    /// 
    /// Griglia a hash spaziale 2D per accelerare la ricerca dei vicini.
    /// 
    /// Problema che risolve:
    /// - Calcolare repulsione tra tutti i nodi è O(N^2) (impossibile a 700-2000).
    /// 
    /// Idea:
    /// - Suddividiamo lo spazio in celle quadrate (cellSize).
    /// - Ogni nodo viene inserito nella cella corrispondente alla sua posizione.
    /// - Per un nodo A cerchiamo possibili vicini solo nelle 9 celle (3x3) attorno ad A.
    /// 
    /// Nota importante:
    /// - La griglia NON calcola distanze, non filtra overlap, non deduplica.
    /// - È solo un "acceleratore" che riduce i confronti, poi la logica vera è nel solver.
    /// </summary>
    [Serializable]
    public class SpatialHashGrid2D
    {
        public float CellSize => _cellSize;

        // Celle -> lista di ID
        private readonly Dictionary<Vector2Int, List<int>> _cells = new Dictionary<Vector2Int, List<int>>(1024);

        // Buffer riusato per QueryNeighbors (evita GC). NON conservarlo fuori dalla chiamata.
        private readonly List<int> _neighborBuffer = new List<int>(256);

        private float _cellSize = 256f;

        /// <summary>
        /// Imposta la dimensione delle celle. Min 1.
        /// </summary>
        public void SetCellSize(float cellSize)
        {
            _cellSize = Mathf.Max(1f, cellSize);
        }

        /// <summary>
        /// Svuota tutte le celle (ma mantiene le liste allocate, evitando garbage).
        /// </summary>
        public void Clear()
        {
            foreach (var kv in _cells)
                kv.Value.Clear();
        }

        /// <summary>
        /// Aggiunge un nodo alla cella corrispondente alla sua posizione.
        /// </summary>
        public void Add(int nodeId, Vector2 position)
        {
            Vector2Int cell = WorldToCell(position);

            if (!_cells.TryGetValue(cell, out var list))
            {
                list = new List<int>(32);
                _cells[cell] = list;
            }

            list.Add(nodeId);
        }

        /// <summary>
        /// Ritorna gli ID dei nodi presenti nelle 9 celle (3x3) attorno alla cella della posizione.
        /// 
        /// ATTENZIONE:
        /// - Restituisce un buffer riusato internamente.
        /// - Non conservarlo in campo, usalo subito e basta.
        /// </summary>
        public List<int> QueryNeighbors(Vector2 position)
        {
            _neighborBuffer.Clear();

            Vector2Int center = WorldToCell(position);

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    Vector2Int c = new Vector2Int(center.x + dx, center.y + dy);

                    if (_cells.TryGetValue(c, out var list) && list.Count > 0)
                        _neighborBuffer.AddRange(list);
                }
            }

            return _neighborBuffer;
        }

        /// <summary>
        /// Converte posizione (anchoredPosition) in coordinate di cella.
        /// </summary>
        public Vector2Int WorldToCell(Vector2 position)
        {
            int cx = Mathf.FloorToInt(position.x / _cellSize);
            int cy = Mathf.FloorToInt(position.y / _cellSize);
            return new Vector2Int(cx, cy);
        }
    }
}
