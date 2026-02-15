using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Arcontio.Core.Logging
{
    public static class ArcontioLogger
    {
        private static bool _initialized;
        private static GameParams _params;
        private static LocalizationDb _loc;
        private static LogTheme _theme;

        private static ILogSink _unitySink;
        private static FileSink _fileSink;
        private static ILogSink _overlaySink;

        private static LogLevel _minLevel = LogLevel.Info;
        private static HtmlFileSink _htmlSink;

        public static bool Initialized => _initialized;
        public static string CurrentLanguage => _params?.Language ?? "it";
        public static string CurrentLogFilePath => _htmlSink?.FilePath ?? _fileSink?.FilePath;

        public static void InitFromResources(
            string gameParamsPathNoExt = "Arcontio/Config/game_params",
            string localizationPathNoExt = "Arcontio/Config/localization_logs")
        {
            if (_initialized) return;

            _params = GameParamsLoader.LoadFromResources(gameParamsPathNoExt);
            _loc = LocalizationDb.LoadFromResources(localizationPathNoExt);
            _theme = new LogTheme();

            _minLevel = ParseLevel(_params.Logging.MinLevel, LogLevel.Info);

            if (_params.Logging.WriteUnityConsole)
            {
                _unitySink = new UnityConsoleSink();

                // Overlay sink (in-game)
                _overlaySink = new UnityOverlaySink();
            }
            //_unitySink = new UnityConsoleSink();

            if (_params.Logging.WriteFile)
            {
                var fileName = BuildFileName(_params.Logging.FileNamePattern);
                var folder = Path.Combine(Application.persistentDataPath, "Logs");
                var path = Path.Combine(folder, fileName);

                var fmt = (_params.Logging.FileFormat ?? "txt").Trim().ToLowerInvariant();
                if (fmt == "html")
                {
                    _htmlSink = new HtmlFileSink(path);
                }
                else
                {
                    _fileSink = new FileSink(path);
                }
            }

            _initialized = true;

            Info(new LogContext(0, "Core"), new LogBlock(LogLevel.Info, "log.sim.start")
                .AddField("lang", CurrentLanguage)
                .AddField("file", _fileSink != null ? _fileSink.FilePath : "disabled"));
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            try { _htmlSink?.Dispose(); } catch { }
            try { _fileSink?.Dispose(); } catch { }
            try { _unitySink?.Dispose(); } catch { }
            try { _overlaySink?.Dispose(); } catch { }
            
            _overlaySink = null;
            _htmlSink = null;
            _fileSink = null;
            _unitySink = null;
            _initialized = false;
        }

        public static void Flush()
        {
            _htmlSink?.Flush();
            _fileSink?.Flush();
        }

        // API rapida
        public static void Trace(LogContext c, LogBlock b) => Write(LogLevel.Trace, c, b);
        public static void Debug(LogContext c, LogBlock b) => Write(LogLevel.Debug, c, b);
        public static void Info(LogContext c, LogBlock b) => Write(LogLevel.Info, c, b);
        public static void Warn(LogContext c, LogBlock b) => Write(LogLevel.Warn, c, b);
        public static void Error(LogContext c, LogBlock b) => Write(LogLevel.Error, c, b);
        public static void Fatal(LogContext c, LogBlock b) => Write(LogLevel.Fatal, c, b);

        private static void Write(LogLevel lvl, LogContext ctx, LogBlock block)
        {
            if (!_initialized) InitFromResources(); // fallback safe
            if (lvl < _minLevel) return;

            block.Level = lvl;

            var msg = !string.IsNullOrEmpty(block.MessageFallback)
                ? block.MessageFallback
                : _loc.Get(block.MessageKey, CurrentLanguage);

            var includeTs = _params.Logging.IncludeTimestamp;
            var includeTick = _params.Logging.IncludeTick;

            // Unity (rich)
            if (_unitySink != null)
            {
                var rich = LogFormat.FormatUnityRich(block, ctx, msg, includeTs, includeTick, _theme);
                _unitySink.Write(rich);
                _overlaySink?.Write(rich);
            }
            if (_htmlSink != null)
            {
                var html = LogFormat.FormatHtmlBlock(block, ctx, msg, includeTs, includeTick);
                _htmlSink.Write(html);
            }
            // File (plain)
            if (_fileSink != null)
            {
                var plain = LogFormat.FormatPlain(block, ctx, msg, includeTs, includeTick);
                _fileSink.Write(plain);
            }
        }

        private static LogLevel ParseLevel(string s, LogLevel fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            s = s.Trim().ToLowerInvariant();
            return s switch
            {
                "trace" => LogLevel.Trace,
                "debug" => LogLevel.Debug,
                "info" => LogLevel.Info,
                "warn" => LogLevel.Warn,
                "error" => LogLevel.Error,
                "fatal" => LogLevel.Fatal,
                _ => fallback
            };
        }

        private static string BuildFileName(string pattern)
        {
            // pattern: "arcontio_{yyyyMMdd_HHmmss}.txt"
            if (string.IsNullOrWhiteSpace(pattern))
                pattern = "arcontio_{yyyyMMdd_HHmmss}.txt";

            var now = DateTime.Now;
            var start = pattern.IndexOf('{');
            var end = pattern.IndexOf('}');
            if (start >= 0 && end > start)
            {
                var fmt = pattern.Substring(start + 1, end - start - 1);
                var stamp = now.ToString(fmt, CultureInfo.InvariantCulture);
                return pattern.Substring(0, start) + stamp + pattern.Substring(end + 1);
            }
            return pattern;
        }
    }
}
