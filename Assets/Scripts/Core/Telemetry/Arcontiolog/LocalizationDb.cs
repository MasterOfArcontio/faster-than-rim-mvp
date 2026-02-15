using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Core.Logging
{
    [Serializable]
    public sealed class LocalizationEntry
    {
        public string key;
        public string it;
        public string en;
    }

    [Serializable]
    public sealed class LocalizationDb
    {
        public LocalizationEntry[] entries;

        private Dictionary<string, LocalizationEntry> _map;

        public void BuildIndex()
        {
            _map = new Dictionary<string, LocalizationEntry>(StringComparer.Ordinal);
            if (entries == null) return;
            foreach (var e in entries)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.key)) continue;
                _map[e.key] = e;
            }
        }

        public string Get(string key, string lang)
        {
            if (_map == null) BuildIndex();
            if (string.IsNullOrWhiteSpace(key)) return "";

            if (_map != null && _map.TryGetValue(key, out var e) && e != null)
            {
                lang = (lang ?? "it").ToLowerInvariant();
                if (lang.StartsWith("en")) return string.IsNullOrEmpty(e.en) ? e.it : e.en;
                return string.IsNullOrEmpty(e.it) ? e.en : e.it;
            }
            // fallback: se manca la key, logghiamo la key stessa (utile in dev)
            return key;
        }

        public static LocalizationDb LoadFromResources(string resourcesPathNoExt)
        {
            var ta = Resources.Load<TextAsset>(resourcesPathNoExt);
            if (ta == null) return new LocalizationDb { entries = Array.Empty<LocalizationEntry>() };

            try
            {
                var db = JsonUtility.FromJson<LocalizationDb>(ta.text) ?? new LocalizationDb();
                db.BuildIndex();
                return db;
            }
            catch
            {
                return new LocalizationDb { entries = Array.Empty<LocalizationEntry>() };
            }
        }
    }
}
