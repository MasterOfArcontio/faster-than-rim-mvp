using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Arcontio.Core.Logging
{
    public enum LogLevel { Trace = 0, Debug = 1, Info = 2, Warn = 3, Error = 4, Fatal = 5 }

    public readonly struct LogContext
    {
        public readonly long Tick;
        public readonly string Channel;   // es: "Sim", "World", "Memory", "Tokens", "UI"
        public readonly int NpcId;        // opzionale (0 se non applicabile)
        public readonly (int x, int y)? Cell; // opzionale
        public LogContext(long tick, string channel, int npcId = 0, (int, int)? cell = null)
        {
            Tick = tick;
            Channel = channel ?? "Core";
            NpcId = npcId;
            Cell = cell;
        }
    }

    public sealed class LogBlock
    {
        public LogLevel Level;
        public string MessageKey;                 // chiave localizzata (es: "log.event.emitted")
        public string MessageFallback;            // fallback raw se vuoi bypassare loc
        public readonly List<(string key, string value)> Fields = new();
        public readonly List<string> Lines = new();
        public Exception Exception;

        public LogBlock(LogLevel level, string messageKey = null, string messageFallback = null)
        {
            Level = level;
            MessageKey = messageKey;
            MessageFallback = messageFallback;
        }

        public LogBlock AddField(string key, object value)
        {
            Fields.Add((key ?? "", value?.ToString() ?? "null"));
            return this;
        }

        public LogBlock AddLine(string line)
        {
            Lines.Add(line ?? "");
            return this;
        }

        public LogBlock WithException(Exception ex)
        {
            Exception = ex;
            return this;
        }
    }

    internal static class LogFormat
    {
        private static string E(string s) => WebUtility.HtmlEncode(s ?? "");

        private static string CssLevelClass(LogLevel lvl) => lvl switch
        {
            LogLevel.Trace => "trace",
            LogLevel.Debug => "debug",
            LogLevel.Info => "info",
            LogLevel.Warn => "warn",
            LogLevel.Error => "error",
            LogLevel.Fatal => "fatal",
            _ => "info"
        };

        public static string FormatPlain(LogBlock b, LogContext ctx, string message, bool includeTs, bool includeTick)
        {
            var sb = new StringBuilder(256);
            var ts = includeTs ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") : null;

            // Header
            // Esempio:
            // 2026-02-13 22:10:12.123 [INFO] [Tick:1234] [World] Evento emesso
            if (includeTs) sb.Append(ts).Append(' ');
            sb.Append('[').Append(b.Level.ToString().ToUpperInvariant()).Append(']').Append(' ');
            if (includeTick) sb.Append("[Tick:").Append(ctx.Tick).Append("] ");
            sb.Append('[').Append(ctx.Channel).Append(']').Append(' ');
            sb.Append(message);

            if (ctx.NpcId != 0) sb.Append("  npc=").Append(ctx.NpcId);
            if (ctx.Cell.HasValue) sb.Append("  cell=(").Append(ctx.Cell.Value.x).Append(',').Append(ctx.Cell.Value.y).Append(')');

            sb.Append('\n');

            // Fields (indent)
            for (int i = 0; i < b.Fields.Count; i++)
            {
                var (k, v) = b.Fields[i];
                sb.Append("  - ").Append(k).Append(": ").Append(v).Append('\n');
            }

            // Extra lines (indent)
            for (int i = 0; i < b.Lines.Count; i++)
            {
                sb.Append("  ").Append(b.Lines[i]).Append('\n');
            }

            // Exception
            if (b.Exception != null)
            {
                sb.Append("  ! exception: ").Append(b.Exception.GetType().Name).Append('\n');
                sb.Append("  ").Append(b.Exception.Message).Append('\n');
                sb.Append(b.Exception.StackTrace).Append('\n');
            }

            return sb.ToString();
        }

        public static string FormatUnityRich(LogBlock b, LogContext ctx, string message, bool includeTs, bool includeTick, LogTheme theme)
        {
            // Unity rich text: <color=#RRGGBB>...</color>
            string Color(string hex, string s) => $"<color=#{hex}>{s}</color>";

            var ts = includeTs ? DateTime.Now.ToString("HH:mm:ss.fff") : null;
            var lvl = b.Level.ToString().ToUpperInvariant();

            var lvlColor = theme.LevelColor(b.Level);
            var chColor = theme.ChannelColor;
            var keyColor = theme.KeyColor;
            var valColor = theme.ValueColor;

            var sb = new StringBuilder(256);

            if (includeTs) sb.Append(Color(theme.TimeColor, ts)).Append(' ');
            sb.Append(Color(lvlColor, $"[{lvl}]")).Append(' ');
            if (includeTick) sb.Append(Color(theme.TickColor, $"[T:{ctx.Tick}]")).Append(' ');
            sb.Append(Color(chColor, $"[{ctx.Channel}]")).Append(' ');
            sb.Append(message);

            if (ctx.NpcId != 0) sb.Append(' ').Append(Color(theme.MetaColor, $"npc={ctx.NpcId}"));
            if (ctx.Cell.HasValue) sb.Append(' ').Append(Color(theme.MetaColor, $"cell=({ctx.Cell.Value.x},{ctx.Cell.Value.y})"));

            // Fields
            for (int i = 0; i < b.Fields.Count; i++)
            {
                var (k, v) = b.Fields[i];
                sb.Append('\n').Append("  ")
                  .Append(Color(keyColor, k)).Append(": ")
                  .Append(Color(valColor, v));
            }

            // Lines
            for (int i = 0; i < b.Lines.Count; i++)
                sb.Append('\n').Append("  ").Append(b.Lines[i]);

            // Exception (compatto)
            if (b.Exception != null)
                sb.Append('\n').Append("  ").Append(Color(theme.ErrorDetailColor, b.Exception.ToString()));

            return sb.ToString();
        }

        public static string FormatHtmlBlock(LogBlock b, LogContext ctx, string message, bool includeTs, bool includeTick)
        {
            var sb = new StringBuilder(512);
            var cls = CssLevelClass(b.Level);

            var ts = includeTs ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") : "";
            var lvl = b.Level.ToString().ToUpperInvariant();

            sb.Append("<div class='log ").Append(cls).Append("'>");

            // Header row
            sb.Append("<div class='hdr'>");
            if (includeTs) sb.Append("<span class='time'>").Append(E(ts)).Append("</span>");
            sb.Append("<span class='lvl'>").Append(E(lvl)).Append("</span>");
            if (includeTick) sb.Append("<span class='tick'>T:").Append(ctx.Tick).Append("</span>");
            sb.Append("<span class='ch'>").Append(E(ctx.Channel)).Append("</span>");
            sb.Append("<span class='msg'>").Append(E(message)).Append("</span>");

            // meta inline
            if (ctx.NpcId != 0) sb.Append("<span class='meta'>npc=").Append(ctx.NpcId).Append("</span>");
            if (ctx.Cell.HasValue) sb.Append("<span class='meta'>cell=(").Append(ctx.Cell.Value.x).Append(',').Append(ctx.Cell.Value.y).Append(")</span>");

            sb.Append("</div>"); // hdr

            // Body: fields
            if (b.Fields.Count > 0 || b.Lines.Count > 0 || b.Exception != null)
            {
                sb.Append("<div class='body'>");

                if (b.Fields.Count > 0)
                {
                    sb.Append("<div class='fields'>");
                    for (int i = 0; i < b.Fields.Count; i++)
                    {
                        var (k, v) = b.Fields[i];
                        sb.Append("<div class='field'>")
                          .Append("<span class='k'>").Append(E(k)).Append("</span>")
                          .Append("<span class='sep'>:</span>")
                          .Append("<span class='v'>").Append(E(v)).Append("</span>")
                          .Append("</div>");
                    }
                    sb.Append("</div>");
                }

                // Extra lines (preformatted)
                if (b.Lines.Count > 0)
                {
                    sb.Append("<pre class='lines'>");
                    for (int i = 0; i < b.Lines.Count; i++)
                    {
                        sb.Append(E(b.Lines[i]));
                        if (i < b.Lines.Count - 1) sb.Append('\n');
                    }
                    sb.Append("</pre>");
                }

                if (b.Exception != null)
                {
                    sb.Append("<pre class='ex'>").Append(E(b.Exception.ToString())).Append("</pre>");
                }

                sb.Append("</div>"); // body
            }

            sb.Append("</div>\n"); // log block + newline
            return sb.ToString();
        }

    }

    public sealed class LogTheme
    {
        public string TimeColor = "7A7A7A";
        public string TickColor = "9AA6FF";
        public string ChannelColor = "6EC6FF";
        public string KeyColor = "FFD166";
        public string ValueColor = "EDEDED";
        public string MetaColor = "A0A0A0";
        public string ErrorDetailColor = "FF6B6B";

        public string LevelColor(LogLevel lvl) => lvl switch
        {
            LogLevel.Trace => "9E9E9E",
            LogLevel.Debug => "BDBDBD",
            LogLevel.Info => "8BC34A",
            LogLevel.Warn => "FFB300",
            LogLevel.Error => "FF5252",
            LogLevel.Fatal => "FF1744",
            _ => "FFFFFF"
        };
    }
}
