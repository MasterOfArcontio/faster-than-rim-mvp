using System;

namespace Arcontio.Core
{
    /// <summary>
    /// OcclusionMap: cache grid per query velocissime di blocco vista/movimento.
    ///
    /// Perché serve:
    /// - LOS e Perception devono controllare tante celle spesso.
    /// - scandire World.Objects ogni volta è costoso.
    ///
    /// Nota:
    /// - Questa è una CACHE. La sorgente di verità restano gli oggetti (World.Objects + ObjectDefs).
    /// - Va ricostruita quando cambiano gli oggetti "Structure" che bloccano.
    /// </summary>
    public sealed class OcclusionMap
    {
        private readonly int _w;
        private readonly int _h;

        // Blocchi (0=libero, 1=blocco)
        private readonly bool[] _blocksVision;
        private readonly bool[] _blocksMove;

        // Costo vista opzionale (>=1). Se non usi “VisionCost”, puoi ignorarlo.
        private readonly float[] _visionCost;

        public int Width => _w;
        public int Height => _h;

        public OcclusionMap(int width, int height)
        {
            _w = Math.Max(1, width);
            _h = Math.Max(1, height);

            int n = _w * _h;
            _blocksVision = new bool[n];
            _blocksMove = new bool[n];
            _visionCost = new float[n];

            Clear();
        }

        public void Clear()
        {
            Array.Fill(_blocksVision, false);
            Array.Fill(_blocksMove, false);
            Array.Fill(_visionCost, 1f);
        }

        public bool InBounds(int x, int y) => (uint)x < (uint)_w && (uint)y < (uint)_h;

        private int Idx(int x, int y) => y * _w + x;

        public void SetCell(int x, int y, bool blocksVision, bool blocksMove, float visionCost)
        {
            if (!InBounds(x, y)) return;
            int i = Idx(x, y);
            _blocksVision[i] = blocksVision;
            _blocksMove[i] = blocksMove;
            _visionCost[i] = (visionCost <= 0f) ? 1f : visionCost;
        }

        public bool BlocksVisionAt(int x, int y)
        {
            if (!InBounds(x, y)) return true; // out-of-bounds = muro
            return _blocksVision[Idx(x, y)];
        }

        public bool BlocksMoveAt(int x, int y)
        {
            if (!InBounds(x, y)) return true;
            return _blocksMove[Idx(x, y)];
        }

        public float VisionCostAt(int x, int y)
        {
            if (!InBounds(x, y)) return 999f;
            return _visionCost[Idx(x, y)];
        }
    }
}
