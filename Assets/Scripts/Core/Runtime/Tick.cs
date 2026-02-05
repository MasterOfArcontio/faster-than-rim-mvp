namespace Arcontio.Core
{
    /// <summary>
    /// Tick: rappresenta l'unità temporale discreta del simulatore.
    /// Usiamo step deterministici (es. 1 tick = 1 minuto di gioco, o 1 secondo simulato).
    /// </summary>
    public readonly struct Tick
    {
        public readonly long Index;
        public readonly float DeltaTime; // tempo simulato per tick (non frame-time)

        public Tick(long index, float deltaTime)
        {
            Index = index;
            DeltaTime = deltaTime;
        }

        public override string ToString() => $"Tick {Index} (dt={DeltaTime})";
    }
}
