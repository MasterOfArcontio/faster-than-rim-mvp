using System;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// ObjectProperty:
    /// Coppia chiave/valore serializzabile.
    ///
    /// Perché non Dictionary?
    /// - JsonUtility di Unity non serializza Dictionary in modo semplice.
    /// - List di key/value è leggibile da JSON e stabile.
    ///
    /// Esempi key:
    /// - "SleepRegenMultiplier"  (float)
    /// - "ComfortBonus"         (float)
    /// - "AllowsAction.Sleep"   (int 0/1)
    /// - "WorkSpeedMultiplier"  (float)
    /// </summary>
    [Serializable]
    /// <summary>
    /// ObjectProperty: identificatore atomico di una proprietà possibile per una definizione oggetto.
    /// Nota: deve esistere UNA sola volta nel progetto.
    /// </summary>
    public enum ObjectProperty
    {
        None = 0,

        // Sleep / comfort
        SleepSpot,
        SleepQuality01,

        // Work
        Workbench,
        WorkSpeedBonus01,

        // Seating
        Chair,
        ComfortBonus01,

        // Ownership
        Ownable,
    }

    /// <summary>
    /// ObjectProperties: contenitore di proprietà/valori.
    /// Nota: questo NON deve chiamarsi ObjectProperty (singolare) per evitare collisioni.
    /// </summary>
    [Serializable]
    public struct ObjectProperties
    {
        public List<ObjectPropertyValue> Values;

        public bool Has(ObjectProperty p)
        {
            if (Values == null) return false;
            for (int i = 0; i < Values.Count; i++)
                if (Values[i].Key == p)
                    return true;
            return false;
        }

        public float Get(ObjectProperty p, float fallback = 0f)
        {
            if (Values == null) return fallback;
            for (int i = 0; i < Values.Count; i++)
                if (Values[i].Key == p)
                    return Values[i].Value;
            return fallback;
        }
    }

    /// <summary>
    /// ObjectPropertyValue: coppia Key/Value serializzabile.
    /// - Usiamo List invece di Dictionary perché è più leggibile in JSON e stabile.
    /// </summary>
    [Serializable]
    public struct ObjectPropertyValue
    {
        public ObjectProperty Key;
        public float Value;
    }
}
