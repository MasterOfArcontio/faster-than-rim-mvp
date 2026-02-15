using System;
using System.Threading;

namespace Arcontio.Core
{
    /// <summary>
    /// TickContext:
    /// Contesto globale del tick corrente.
    ///
    /// Perché esiste:
    /// - Molte classi (Commands, Systems, ecc.) non ricevono Tick direttamente.
    /// - Non vogliamo “propagare tick” in tutti i costruttori.
    /// - In Unity giri quasi sempre sul main thread: ThreadStatic va bene.
    ///
    /// Uso:
    /// - SimulationHost setta BeginTick(...) all’inizio di StepOneTick.
    /// - Qualunque codice può leggere CurrentTickIndex per logging.
    /// </summary>
    public static class TickContext
    {
        [ThreadStatic] private static long _currentTickIndex;

        public static long CurrentTickIndex => _currentTickIndex;

        public static void BeginTick(long tickIndex)
        {
            _currentTickIndex = tickIndex;
        }
    }
}
