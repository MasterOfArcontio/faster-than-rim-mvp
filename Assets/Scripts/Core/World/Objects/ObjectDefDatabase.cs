using System;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// ObjectPropertyKV:
    /// Coppia Key/Value serializzabile per JsonUtility.
    /// Deve matchare il JSON: { "Key": "...", "Value": 1.0 }
    /// </summary>
    [Serializable]
    public struct ObjectPropertyKV
    {
        public string Key;
        public float Value;
    }

    /// <summary>
    /// ObjectDef: definizione data-driven di un oggetto del mondo.
    /// Nota: NON contiene Sprite Unity, solo SpriteKey (string) risolto dalla View.
    /// </summary>
    [Serializable]
    public sealed class ObjectDef
    {
        public string Id;
        public string DisplayName;

        public string SpriteKey;

        // Classificazione logica
        public bool IsOccluder;       // se true: entra nella occlusion map
        public bool IsInteractable;   // se true: può finire nella “memoria oggetti interagibili”

        // Occlusion params (validi se IsOccluder=true)
        public bool BlocksVision;
        public bool BlocksMovement;
        public float VisionCost;

        // Distruzione (validi se IsOccluder=true, opzionali)
        public int MaxHp;
        public float Hardness;

        // Proprietà generiche (letto, workbench, food, ecc.)
        public List<ObjectPropertyKV> Properties;
    }

    /// <summary>
    /// Root del JSON: { "Objects": [ ... ] }
    /// </summary>
    [Serializable]
    public sealed class ObjectDefDatabase
    {
        public List<ObjectDef> Objects;
    }
}
