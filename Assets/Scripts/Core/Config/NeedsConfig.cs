using System;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// NeedsConfig (Day9):
    /// Parametri standard (data-driven) per fame/sonno.
    ///
    /// Convenzione:
    /// - Hunger01 e Fatigue01 sono in [0..1]
    /// - 0 = ok, 1 = critico
    ///
    /// satietyDecayPerTick:
    /// - quanto cresce Hunger01 ad ogni tick (più alto = più fame)
    ///
    /// restDecayPerTick:
    /// - quanto cresce Fatigue01 ad ogni tick (più alto = più stanchezza)
    ///
    /// eatSatietyGain:
    /// - quanto diminuisce Hunger01 quando mangi 1 unità
    ///
    /// sleepRestGainPerTick:
    /// - quanto diminuisce Fatigue01 per tick quando dorme (v0: “instant” 1 tick)
    ///
    /// hungryThreshold/tiredThreshold:
    /// - sopra queste soglie scatta l'intento decisionale.
    /// </summary>
    [Serializable]
    public struct NeedsConfig
    {
        public float satietyDecayPerTick;
        public float restDecayPerTick;

        public float eatSatietyGain;
        public float sleepRestGainPerTick;

        public float hungryThreshold;
        public float tiredThreshold;

        public static NeedsConfig Default()
        {
            return new NeedsConfig
            {
                satietyDecayPerTick = 0.005f,
                restDecayPerTick = 0.004f,
                eatSatietyGain = 0.40f,
                sleepRestGainPerTick = 0.35f,
                hungryThreshold = 0.70f,
                tiredThreshold = 0.70f
            };
        }

        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>
    /// Wrapper serializzabile per JsonUtility.
    /// JSON atteso:
    /// {
    ///   "Needs": { ... }
    /// }
    /// </summary>
    [Serializable]
    public sealed class NeedsConfigDatabase
    {
        public NeedsConfig Needs;
    }
}
