using Arcontio.Core;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// Punto unico dove la View recupera il World del core.
    /// Se cambierà la struttura (es. service locator, DI, etc.) modifichi SOLO qui.
    /// </summary>
    public static class MapGridWorldProvider
    {
        public static World TryGetWorld()
        {
            var host = SimulationHost.Instance;
            if (host == null) return null;
            return host.World;
        }
    }
}
