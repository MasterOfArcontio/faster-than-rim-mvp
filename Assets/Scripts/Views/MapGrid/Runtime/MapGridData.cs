namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// Dati runtime della MapGrid (solo view-data).
    ///
    /// Contiene lo stato "a celle" minimo:
    /// - tileId terreno per cella
    /// - flag bloccato (ostacolo)
    ///
    /// IMPORTANTISSIMO:
    /// - Questo NON è il World del simulatore.
    /// - È un buffer di dati che la view usa per renderizzare.
    /// - La sorgente può essere:
    ///   - layout JSON (ora)
    ///   - simulatore (poi)
    ///   - procedurale (poi)
    /// </summary>
    public sealed class MapGridData
    {
        public readonly int Width;
        public readonly int Height;

        private readonly int[] _terrain;
        private readonly bool[] _blocked;

        public MapGridData(int w, int h)
        {
            Width = w;
            Height = h;

            _terrain = new int[w * h];
            _blocked = new bool[w * h];
        }

        /// <summary>
        /// Check bounds in modo veloce e sicuro.
        /// (uint trick evita branch su negativo)
        /// </summary>
        public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

        /// <summary>
        /// Converte (x,y) in indice lineare.
        /// Convenzione: row-major (x + y*Width).
        /// </summary>
        public int Index(int x, int y) => x + y * Width;

        public int GetTerrain(int x, int y) => _terrain[Index(x, y)];
        public void SetTerrain(int x, int y, int tileId) => _terrain[Index(x, y)] = tileId;

        public bool IsBlocked(int x, int y) => _blocked[Index(x, y)];
        public void SetBlocked(int x, int y, bool v) => _blocked[Index(x, y)] = v;
    }
}
