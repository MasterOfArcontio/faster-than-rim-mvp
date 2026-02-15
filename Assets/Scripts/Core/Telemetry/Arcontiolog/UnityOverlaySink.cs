using Arcontio.Core.Logging;
using Arcontio.View;
using System;

public sealed class UnityOverlaySink : ILogSink
{
    public void Write(string message)
    {
        ArcontioLogOverlay.Enqueue(message);
    }

    public void Flush()
    {
        // No-op: l'overlay aggiorna a runtime
    }

    public void Dispose()
    {
        // No-op
    }
}
