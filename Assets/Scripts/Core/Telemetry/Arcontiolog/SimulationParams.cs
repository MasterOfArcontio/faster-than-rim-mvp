using System;
using UnityEngine;

namespace Arcontio.Core.Config
{
    [Serializable]
    public sealed class SimulationParams
    {
        // Esempi (metti qui quelli veri)
        public int worldWidth = 64;
        public int worldHeight = 64;
        public string Language = "it";
    }
        
    public static class SimulationParamsLoader
    {
        public static SimulationParams LoadFromResources(string resourcesPathNoExt)
        {
            var ta = Resources.Load<TextAsset>(resourcesPathNoExt);
            if (ta == null)
            {
                Debug.LogWarning($"[Arcontio] Missing sim params at Resources/{resourcesPathNoExt}.json. Using defaults.");
                return new SimulationParams();
            }

            try
            {
                return JsonUtility.FromJson<SimulationParams>(ta.text) ?? new SimulationParams();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Arcontio] Failed parsing sim params: {ex}");
                return new SimulationParams();
            }
        }
    }
}
