namespace Arcontio.Core
{
    /// <summary>
    /// SpatialQuantizer:
    /// Converte una cella (x,y) in una "macro-cella" (regionX, regionY)
    /// per fare fusione spaziale delle memorie.
    ///
    /// Esempio:
    /// - regionSize=4
    /// - (0..3, 0..3) -> region (0,0)
    /// - (4..7, 0..3) -> region (1,0)
    /// </summary>
    public static class SpatialQuantizer
    {
        public static void QuantizeCell(int cellX, int cellY, int regionSize, out int regionX, out int regionY)
        {
            if (regionSize <= 1)
            {
                // regionSize=1 => nessuna fusione (identità)
                regionX = cellX;
                regionY = cellY;
                return;
            }

            // Divisione intera: 0..3 => 0, 4..7 => 1, ecc.
            regionX = cellX / regionSize;
            regionY = cellY / regionSize;
        }
    }
}
