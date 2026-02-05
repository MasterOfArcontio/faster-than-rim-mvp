using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace Arcontio.Core
{
    /// <summary>
    /// ViewSwitcher basato su InputActions asset (New Input System).
    /// Va messo su ArcontioRuntime (DontDestroyOnLoad), così funziona in qualunque scena.
    /// </summary>
    public sealed class ViewSwitcherInputActions : MonoBehaviour
    {
        [Header("Scene names (devono essere in Build Settings)")]
        [SerializeField] private string atomViewerSceneName = "Scene_AtomViewer";
        [SerializeField] private string mapGridName = "Scene_MapGrid";

        // 1) QUI: sostituisci con il nome della classe generata dal tuo .inputactions
        private ArcontioInputActions _actions;

        private void Awake()
        {
            Debug.Log("[ViewSwitcher] Awake START");
            _actions = new ArcontioInputActions();
        }

        private void OnEnable()
        {
            _actions.Enable();

            // 2) QUI: sostituisci "Global" e i nomi delle azioni se li hai chiamati diversamente
            _actions.Global.SwitchToAtomViewer.performed += OnSwitchToAtomViewer;
            _actions.Global.SwitchToMap.performed += OnSwitchToMap;
        }

        private void OnDisable()
        {
            if (_actions == null) return;

            _actions.Global.SwitchToAtomViewer.performed -= OnSwitchToAtomViewer;
            _actions.Global.SwitchToMap.performed -= OnSwitchToMap;

            _actions.Disable();
        }

        private void OnSwitchToAtomViewer(InputAction.CallbackContext ctx) => LoadIfNotActive(atomViewerSceneName);
        private void OnSwitchToMap(InputAction.CallbackContext ctx) => LoadIfNotActive(mapGridName);

        private static void LoadIfNotActive(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (SceneManager.GetActiveScene().name == sceneName)
                return;

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[ViewSwitcher] Scene '{sceneName}' not in Build Profile/Scenes list.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

    }
}
