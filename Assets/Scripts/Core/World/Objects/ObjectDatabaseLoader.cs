using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// ObjectDatabaseLoader:
    /// Carica definizioni oggetti da JSON (Resources).
    ///
    /// File atteso:
    /// Assets/Resources/Arcontio/Config/object_defs.json
    ///
    /// Nota Unity:
    /// - Resources.Load vuole path RELATIVO a Assets/Resources
    /// - quindi qui usiamo: "Arcontio/Config/object_defs" (senza estensione)
    /// </summary>
    public static class ObjectDatabaseLoader
    {
        private const string ResourcePath = "Arcontio/Config/object_defs"; // NO ".json", NO "Resources/"

        public static void LoadIntoWorld(World world)
        {
            if (world == null) return;

            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta == null)
            {
                Debug.LogWarning($"[ObjectDB] Missing resource at Assets/Resources/{ResourcePath}.json");
                return;
            }

            // Parsing
            var db = JsonUtility.FromJson<ObjectDefDatabase>(ta.text);
            if (db == null || db.Objects == null)
            {
                Debug.LogWarning("[ObjectDB] JSON parsed null/empty. (Root key must be 'Objects')");
                return;
            }

            // World deve avere: public Dictionary<string,ObjectDef> ObjectDefs
            world.ObjectDefs.Clear();

            int added = 0;
            for (int i = 0; i < db.Objects.Count; i++)
            {
                var def = db.Objects[i];
                if (def == null) continue;
                if (string.IsNullOrWhiteSpace(def.Id)) continue;

                world.ObjectDefs[def.Id] = def;
                added++;
            }

            Debug.Log($"[ObjectDB] Loaded object defs: {added}");
        }
    }
}
