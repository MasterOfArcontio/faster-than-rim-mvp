using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using UnityEngine;

namespace Arcontio.Core.Logging
{
    public interface ILogSink : IDisposable
    {
        void Write(string formatted);
        void Flush();
    }

    public sealed class UnityConsoleSink : ILogSink
    {
        public void Write(string formatted)
        {
            // Se vuoi mapparlo a Debug/Warning/Error in base al livello,
            // passa già la stringa con indicatori. Qui faccio Debug.Log per semplicità.
            Debug.Log(formatted);
        }

        public void Flush() { }
        public void Dispose() { }
    }

    public sealed class FileSink : ILogSink
    {
        private readonly string _filePath;
        private readonly ConcurrentQueue<string> _queue = new();
        private StreamWriter _writer;

        public string FilePath => _filePath;

        public FileSink(string filePath)
        {
            _filePath = filePath;
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));
            _writer = new StreamWriter(_filePath, append: true, encoding: new UTF8Encoding(false))
            {
                AutoFlush = false
            };
        }

        public void Write(string formatted) => _queue.Enqueue(formatted);

        public void Flush()
        {
            if (_writer == null) return;

            while (_queue.TryDequeue(out var s))
                _writer.Write(s);

            _writer.Flush();
        }

        public void Dispose()
        {
            try { Flush(); } catch { /* ignore */ }
            try { _writer?.Dispose(); } catch { /* ignore */ }
            _writer = null;
        }
    }
}
