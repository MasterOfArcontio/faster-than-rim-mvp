using UnityEngine;

namespace Arcontio.View.MapGrid
{
    /// <summary>
    /// Utility minimale per caricare JSON da Resources.
    ///
    /// Regola:
    /// - Metti i JSON in Assets/Resources/MapGrid/...
    /// - Passi la path senza estensione, es:
    ///   "MapGrid/Config/MapGridConfig"
    ///
    /// Motivazione:
    /// - Per una grid view, avere config/layout "file-driven" è utilissimo:
    ///   permette di cambiare mappe e parametri senza ricompilare.
    /// </summary>
    public static class MapGridJsonLoader
    {
        public static T LoadFromResources<T>(string resourcePath) where T : class
        {
            var ta = Resources.Load<TextAsset>(resourcePath);
            if (ta == null)
            {
                Debug.LogError($"[MapGrid] Missing JSON TextAsset at Resources/{resourcePath}.json");
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(ta.text);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MapGrid] JSON parse error at Resources/{resourcePath}.json\n{ex}");
                return null;
            }
        }
    }
}
