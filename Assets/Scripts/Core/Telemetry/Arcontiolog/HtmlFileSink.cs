using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Arcontio.Core.Logging
{
    public sealed class HtmlFileSink : ILogSink
    {
        private readonly string _filePath;
        private StreamWriter _writer;
        private bool _started;

        public string FilePath => _filePath;

        public HtmlFileSink(string filePath)
        {
            _filePath = filePath;
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath));

            _writer = new StreamWriter(_filePath, append: false, encoding: new UTF8Encoding(false))
            {
                AutoFlush = false
            };

            StartDocument();
        }

        private void StartDocument()
        {
            if (_started) return;
            _started = true;

            _writer.WriteLine("<!doctype html>");
            _writer.WriteLine("<html><head><meta charset='utf-8'>");
            _writer.WriteLine("<meta name='viewport' content='width=device-width, initial-scale=1'>");
            _writer.WriteLine("<title>Arcontio Log</title>");
            _writer.WriteLine("<style>");
            _writer.WriteLine(@"
                :root{
                    --bg:#0f1115; --fg:#e6e6e6; --muted:#9aa0a6;
                    --trace:#9e9e9e; --debug:#bdbdbd; --info:#8bc34a;
                    --warn:#ffb300; --error:#ff5252; --fatal:#ff1744;
                    --chip:#1c2230; --line:#232a3a;
                }
                body{ margin:0; background:var(--bg); color:var(--fg); font:13px/1.35 ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono','Courier New', monospace; }
                .top{ position:sticky; top:0; background:rgba(15,17,21,.95); border-bottom:1px solid var(--line); padding:10px 12px; z-index:5;}
                .top .hint{ color:var(--muted); font-size:12px; }
                .wrap{ padding:12px; display:flex; flex-direction:column; gap:10px; }
                .log{ border:1px solid var(--line); border-radius:10px; background:#121622; overflow:hidden; }
                .hdr{ display:flex; flex-wrap:wrap; gap:8px; align-items:baseline; padding:8px 10px; border-bottom:1px solid var(--line); }
                .time{ color:var(--muted); }
                .lvl{ font-weight:700; padding:1px 6px; border-radius:999px; background:var(--chip); }
                .tick{ color:#9aa6ff; }
                .ch{ color:#6ec6ff; }
                .msg{ color:var(--fg); }
                .meta{ color:var(--muted); }
                .body{ padding:8px 10px; }
                .fields{ display:flex; flex-direction:column; gap:4px; margin-bottom:8px; }
                .field{ display:flex; gap:6px; }
                .k{ color:#ffd166; min-width:140px; }
                .sep{ color:var(--muted); }
                .v{ color:var(--fg); white-space:pre-wrap; word-break:break-word; }
                pre{ margin:0; white-space:pre-wrap; word-break:break-word; }
                .lines{ color:var(--fg); opacity:.95; padding:8px; border-radius:8px; background:#0f131d; border:1px solid var(--line); }
                .ex{ color:#ffd0d0; padding:8px; border-radius:8px; background:#1a0f14; border:1px solid #3a232c; }

                /* level accents */
                .trace .lvl{ color:var(--trace); }
                .debug .lvl{ color:var(--debug); }
                .info  .lvl{ color:var(--info); }
                .warn  .lvl{ color:var(--warn); }
                .error .lvl{ color:var(--error); }
                .fatal .lvl{ color:var(--fatal); }
                .warn  { box-shadow:0 0 0 1px rgba(255,179,0,.15) inset; }
                .error { box-shadow:0 0 0 1px rgba(255,82,82,.18) inset; }
                .fatal { box-shadow:0 0 0 1px rgba(255,23,68,.20) inset; }
            ");
            _writer.WriteLine("</style></head><body>");
            _writer.WriteLine("<div class='top'><div><b>ARCONTIO LOG</b></div><div class='hint'>Apri questo file in un browser. Ogni blocco è un record; i campi sono strutturati.</div></div>");
            _writer.WriteLine("<div class='wrap'>");
            _writer.Flush();
        }

        public void Write(string formattedHtmlBlock)
        {
            if (_writer == null) return;
            // Qui scriviamo già HTML di un blocco <div class='log ...'>
            _writer.Write(formattedHtmlBlock);
        }

        public void Flush()
        {
            _writer?.Flush();
        }

        public void Dispose()
        {
            if (_writer == null) return;

            try
            {
                _writer.WriteLine("</div></body></html>");
                _writer.Flush();
            }
            catch { /* ignore */ }

            try { _writer.Dispose(); } catch { /* ignore */ }
            _writer = null;
        }
    }
}
