using System;
using System.Collections.Generic;
using UnityEngine;

namespace SocialViewer.UI.Graph
{
    /// <summary>
    /// ArcontioMagnetTestDriver
    /// 
    /// - Legge un file JSON (TextAsset) contenente una sequenza di eventi.
    /// - Ogni evento viene scatenato quando TimeSinceStart supera atTime.
    /// - Serve come ambiente test "simulatore Arcontio" minimale.
    /// 
    /// NOTA:
    /// - Usiamo JsonUtility: per questo il JSON ha un wrapper { "events": [...] }.
    /// </summary>
    public class ArcontioMagnetTestDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GraphMagnetSystem magnetSystem;

        [Header("Test script (JSON)")]
        [Tooltip("TextAsset JSON con wrapper { \"events\": [...] }")]
        [SerializeField] private TextAsset jsonScript;

        [Header("Tick")]
        [Tooltip("Ogni quanto controlliamo/scateniamo eventi (secondi).")]
        //[SerializeField] private float tickInterval = 0.25f;
        [SerializeField] private float tickInterval = 1;

        // =========================
        // Stato
        // =========================
        private bool _isRunning = false;

        private float _time;
        private float _tickAcc;

        private ScriptRoot _script;
        private int _nextEventIndex;

        private void Awake()
        {
            if (magnetSystem == null)
                Debug.LogError("[ArcontioMagnetTestDriver] magnetSystem non assegnato.");

            Debug.Log("[ArcontioMagnetTestDriver] Awake");
   
            LoadScript();
        }

        private void LoadScript()
        {
            if (jsonScript == null)
            {
                Debug.LogWarning("[ArcontioMagnetTestDriver] jsonScript non assegnato: nessun evento verrà eseguito.");
                _script = null;
                return;
            }

            try
            {
                _script = JsonUtility.FromJson<ScriptRoot>(jsonScript.text);
                if (_script == null || _script.events == null)
                {
                    Debug.LogError("[ArcontioMagnetTestDriver] JSON parsing fallito o struttura mancante (events).");
                    _script = null;
                    return;
                }

                // Ordiniamo per tempo per sicurezza (JsonUtility non garantisce ordinamento diverso da input, ma meglio esplicito)
                Array.Sort(_script.events, (a, b) => a.atTime.CompareTo(b.atTime));
                _nextEventIndex = 0;

                Debug.Log($"[ArcontioMagnetTestDriver] Script caricato: {_script.events.Length} eventi.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ArcontioMagnetTestDriver] Errore parsing JSON: {ex.Message}");
                _script = null;
            }
        }

        private void Update()
        {
            if (!_isRunning) return;   // BLOCCO FINCHÉ NON PREMI IL BOTTONE

            if (_script == null || _script.events == null || magnetSystem == null) return;

            float dt = Time.unscaledDeltaTime;
            _time += dt;
            _tickAcc += dt;

            if (_tickAcc < tickInterval) return;
            _tickAcc = 0f;

            // Esegui tutti gli eventi la cui atTime è stata superata
            while (_nextEventIndex < _script.events.Length && _script.events[_nextEventIndex].atTime <= _time)
            {
                ExecuteEvent(_script.events[_nextEventIndex]);
                _nextEventIndex++;
            }
        }

        private void ExecuteEvent(ScriptEvent e)
        {
            // Nota: alcune azioni usano members, altre leaderId, altre solo params.
            switch (e.type)
            {
                case "SetGroup":
                    magnetSystem.SetMagnet(
                        e.magnetKey,
                        e.leaderId,
                        e.members,
                        e.cohesion01,
                        e.attraction01
                    );
                    Debug.Log($"[ArcontioMagnetTestDriver] SetGroup key={e.magnetKey} leader={e.leaderId} members={Len(e.members)}");
                    break;

                case "UpdateParams":
                    magnetSystem.SetMagnetParams(e.magnetKey, e.cohesion01, e.attraction01);
                    Debug.Log($"[ArcontioMagnetTestDriver] UpdateParams key={e.magnetKey} cohesion={e.cohesion01} attr={e.attraction01}");
                    break;

                case "ChangeLeader":
                    magnetSystem.SetMagnetLeader(e.magnetKey, e.leaderId);
                    Debug.Log($"[ArcontioMagnetTestDriver] ChangeLeader key={e.magnetKey} leader={e.leaderId}");
                    break;

                case "AddMembers":
                    magnetSystem.AddMembers(e.magnetKey, e.members);
                    Debug.Log($"[ArcontioMagnetTestDriver] AddMembers key={e.magnetKey} +{Len(e.members)}");
                    break;

                case "RemoveMembers":
                    magnetSystem.RemoveMembers(e.magnetKey, e.members);
                    Debug.Log($"[ArcontioMagnetTestDriver] RemoveMembers key={e.magnetKey} -{Len(e.members)}");
                    break;

                case "ClearGroup":
                    magnetSystem.ClearMagnet(e.magnetKey);
                    Debug.Log($"[ArcontioMagnetTestDriver] ClearGroup key={e.magnetKey}");
                    break;

                case "ClearAll":
                    magnetSystem.ClearAllMagnets();
                    Debug.Log("[ArcontioMagnetTestDriver] ClearAll");
                    break;

                default:
                    Debug.LogWarning($"[ArcontioMagnetTestDriver] Tipo evento sconosciuto: {e.type}");
                    break;
            }
        }

        // =========================
        // API PUBBLICA (per Button)
        // =========================

        public void StartTest()
        {
            ResetTest();
            _isRunning = true;
            Debug.Log("[ArcontioMagnetTestDriver] TEST AVVIATO");
        }

        public void StopTest()
        {
            _isRunning = false;
            Debug.Log("[ArcontioMagnetTestDriver] TEST FERMATO");
        }

        public void ResetTest()
        {
            _time = 0f;
            _tickAcc = 0f;
            _nextEventIndex = 0;
            magnetSystem.ClearAllMagnets();
            Debug.Log("[ArcontioMagnetTestDriver] TEST RESET");
        }

        private static int Len(List<int> list) => list == null ? 0 : list.Count;

        // =========================================================
        //  JSON structs (JsonUtility)
        // =========================================================

        [Serializable]
        private class ScriptRoot
        {
            public ScriptEvent[] events;
        }

        [Serializable]
        private class ScriptEvent
        {
            public float atTime;
            public string type;

            public int magnetKey;
            public int leaderId;

            public float cohesion01;
            public float attraction01;

            // JsonUtility supporta List<int>
            public List<int> members;
        }
    }
}
