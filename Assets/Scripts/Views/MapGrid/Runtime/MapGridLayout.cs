using System;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// DTO layout mappa caricato da JSON.
    ///
    /// Scopo:
    /// - definire UNA "struttura di mappa" caricabile da file:
    ///   - terreno (tileId)
    ///   - ostacoli (celle bloccate)
    ///   - risorse (celle con item)
    ///
    /// Questo non è il World del simulatore: è un input / preset per la view.
    /// In futuro puoi:
    /// - generare layout proceduralmente
    /// - o riceverlo dal simulatore
    /// - o combinarlo (preset + modifiche runtime)
    /// </summary>
    [Serializable]
    public sealed class MapGridLayout
    {
        public int width;
        public int height;

        public TerrainLayout terrain;
        public CellPos[] occluders;
        public ResourcePos[] resources;

        [Serializable]
        public sealed class TerrainLayout
        {
            // Fill di default per tutte le celle (tileId).
            public int fill;

            // Patch rettangolari (riempiono aree con tile specifico).
            public Patch[] patches;

            [Serializable]
            public sealed class Patch
            {
                public int tileId;
                public int x, y, w, h;
            }
        }

        [Serializable]
        public sealed class CellPos
        {
            public int x;
            public int y;
        }

        [Serializable]
        public sealed class ResourcePos
        {
            public string type;
            public int x;
            public int y;
            public int amount;
        }
    }
}
