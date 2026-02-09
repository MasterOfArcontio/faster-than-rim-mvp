using System;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// ObjectKeyValue:
    /// Entry serializzabile per JsonUtility per rappresentare:
    /// { "Key": "...", "Value": 1.0 }
    ///
    /// Nota:
    /// - JsonUtility non gestisce bene Dictionary<,>, quindi usiamo una List di coppie.
    /// - La semantica (float vs flag) la decidi tu in runtime (es. Value>0.5 => true).
    /// </summary>
    [Serializable]
    public sealed class ObjectKeyValue
    {
        public string Key;
        public float Value;
    }

    /// <summary>
    /// ObjectDef:
    /// Definizione statica "data-driven" di un oggetto piazzabile (letto, workbench, sedia...).
    ///
    /// Importante:
    /// - I nomi dei campi devono combaciare con il JSON: Id, DisplayName, Properties.
    /// - Per grafica: SpriteKey/IconKey/VariantSpriteKeys sono opzionali nel JSON.
    /// </summary>
    [Serializable]
    public sealed class ObjectDef
    {
        public string Id;                 // "bed_wood_poor"
        public string DisplayName;        // "Wood Bed (Poor)"

        // Riferimenti grafici (opzionali):
        // La View risolve key->Sprite (Resources/Addressables/altro).
        public string SpriteKey;          // es. "Sprites/Objects/bed_wood_poor"
        public string IconKey;            // es. "Sprites/Icons/bed"
        public string[] VariantSpriteKeys;

        // COME NEL TUO JSON:
        // "Properties": [ { "Key": "...", "Value": 1.0 }, ... ]
        public List<ObjectKeyValue> Properties;
    }

    /// <summary>
    /// ObjectDefDatabase:
    /// Root del JSON.
    ///
    /// Deve avere un campo "Objects" perché nel tuo JSON la chiave root è "Objects".
    /// </summary>
    [Serializable]
    public sealed class ObjectDefDatabase
    {
        public List<ObjectDef> Objects;
    }
}
