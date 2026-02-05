using UnityEngine;
using Arcontio.Core; // <-- serve per SimulationHost/World

namespace SocialViewer.UI.Graph
{
    /// <summary>
    /// GraphNodeSpawner
    ///
    /// Responsabilità (UI bootstrap):
    /// - Genera in runtime i nodi (sfere) sotto NodesRoot.
    /// - Inizializza i componenti dei nodi: ID logico, UI events, drag, forwarder, view.
    /// - Posiziona i nodi in una griglia (per ora).
    ///
    /// Nota architetturale IMPORTANTISSIMA:
    /// - Lo spawner NON contiene logiche di simulazione Arcontio.
    /// - Lo spawner "legge" dati dal simulatore (World) e li traduce in oggetti UI.
    /// - Il simulatore vive su ArcontioRuntime (DontDestroyOnLoad) e continua a tickare tra le scene.
    /// </summary>
    public class GraphNodeSpawner : MonoBehaviour
    {
        [Header("Data source")]
        [Tooltip("Se true, i nodi vengono creati dagli NPC reali del simulatore (SimulationHost.Instance.World).")]
        [SerializeField] private bool useArcontio = true;

        [Tooltip("Se true, quando entri nella scena fa spawn automatico (Arcontio o test).")]
        [SerializeField] private bool spawnOnStart = true;

        [Header("Scene references")]
        [Tooltip("Componente di pan/zoom presente su GraphViewport.")]
        [SerializeField] private GraphPanZoom panZoom;

        [Tooltip("NodesRoot: padre di tutte le sfere (RectTransform).")]
        [SerializeField] private RectTransform nodesRoot;

        [Tooltip("GraphViewport GameObject (receiver degli input forwarding). Se null useremo panZoom.gameObject.")]
        [SerializeField] private GameObject viewportGO;

        [Header("Overlay controllers (OverlayUI)")]
        [SerializeField] private SocialViewer.UI.Overlay.TooltipController tooltip;
        [SerializeField] private SocialViewer.UI.Overlay.ContextMenuController contextMenu;

        [Header("Prefab")]
        [Tooltip("Prefab del nodo (NPCAtomNode). Deve includere i componenti necessari (RectTransform, collision, ecc.).")]
        [SerializeField] private GameObject nodePrefab;

        [Header("Layout (per ora griglia)")]
        [SerializeField] private int cols = 6;
        [SerializeField] private float spacingX = 400f;
        [SerializeField] private float spacingY = 300f;

        [Header("Test spawn (solo se useArcontio = false)")]
        [SerializeField] private int testCount = 54;

        private void Start()
        {
            if (!spawnOnStart) return;

            if (useArcontio)
                SpawnFromArcontio();
            else
                SpawnTest();
        }

        // --------------------------------------------------------------------
        // API pubbliche (comode per debug da Inspector)
        // --------------------------------------------------------------------

        [ContextMenu("Spawn From Arcontio")]
        public void SpawnFromArcontio()
        {
            // ------------------------------------------------------------
            // 0) Guardie (references essenziali)
            // ------------------------------------------------------------
            if (!CheckEssentials()) return;

            // ------------------------------------------------------------
            // 1) Recupero simulatore
            // ------------------------------------------------------------
            // Se avvii la scena AtomView SENZA passare da Bootstrap,
            // SimulationHost.Instance può essere null.
            var host = SimulationHost.Instance;
            if (host == null)
            {
                Debug.LogError(
                    "[GraphNodeSpawner] SimulationHost.Instance è null.\n" +
                    "Cause più comune: hai premuto Play direttamente in Scene_AtomView.\n" +
                    "Soluzione: premi Play in Scene_Bootstrap (dove vive ArcontioRuntime) e poi switcha vista."
                );
                return;
            }

            var world = host.World;
            if (world == null)
            {
                Debug.LogError("[GraphNodeSpawner] host.World è null (non dovrebbe accadere).");
                return;
            }

            // ------------------------------------------------------------
            // 2) Pulisci eventuali nodi pre-esistenti
            // ------------------------------------------------------------
            ClearNodes();

            // ------------------------------------------------------------
            // 3) Loop su NPC reali (World.NpcCore contiene la lista NPC)
            // ------------------------------------------------------------
            // Importante: World.NpcCore è un Dictionary<int, NpcCore>.
            // La chiave è l'ID NPC "reale" del simulatore.
            int safeCols = Mathf.Max(1, cols);
            int iNode = 0;

            foreach (var kv in world.NpcCore)
            {
                int npcId = kv.Key;       // ID REALE (persistente e coerente)
                var core = kv.Value;      // dati base (nome, tratti, ecc.)

                // ------------------------------------------------------------
                // A) Crea nodo UI disattivo (per inizializzare prima di OnEnable)
                // ------------------------------------------------------------
                GameObject go = Instantiate(nodePrefab, nodesRoot);
                go.name = $"NPCAtomNode_{npcId}";
                go.SetActive(false);

                // ------------------------------------------------------------
                // B) RectTransform (servirà per posizionamento)
                // ------------------------------------------------------------
                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt == null)
                {
                    Debug.LogError($"[GraphNodeSpawner] Il prefab '{nodePrefab.name}' non ha RectTransform sul root.");
                    Destroy(go);
                    continue;
                }

                // ------------------------------------------------------------
                // C) Imposta ID sul componente collision PRIMA di attivare
                // ------------------------------------------------------------
                // NPCNodeCollision fa Register() in OnEnable: deve avere l'ID corretto prima.
                var collision = go.GetComponentInChildren<NPCNodeCollision>(true);
                if (collision != null)
                {
                    collision.SetId(npcId);
                }
                else
                {
                    Debug.LogWarning(
                        $"[GraphNodeSpawner] NPCNodeCollision non trovato nel prefab '{go.name}'. " +
                        "Il magnet system / collision registry non potrà usare questo nodo."
                    );
                }

                // ------------------------------------------------------------
                // D) UI Events (tooltip / context menu)
                // ------------------------------------------------------------
                var uiEvents = go.GetComponentInChildren<NPCNodeUIEvents>(true);
                if (uiEvents != null)
                {
                    uiEvents.SetDependencies(tooltip, contextMenu);

                    // Stato iniziale: per ora mettiamo "Idle".
                    // In futuro lo ricavi da Needs/Work/Social del World (bridge di aggiornamento).
                    uiEvents.SetNodeInfo(npcId, core.Name, "Idle");
                }
                else
                {
                    Debug.LogWarning($"[GraphNodeSpawner] NPCNodeUIEvents non trovato sotto '{go.name}'.");
                }

                // ------------------------------------------------------------
                // E) Input forwarding (pan/zoom quando mouse è sopra la sfera)
                // ------------------------------------------------------------
                var forwarder = go.GetComponent<GraphInputForwarder>();
                if (forwarder != null)
                {
                    var receiver = (viewportGO != null)
                        ? viewportGO
                        : (panZoom != null ? panZoom.gameObject : null);

                    if (receiver != null) forwarder.SetViewportReceiver(receiver);
                    else Debug.LogWarning("[GraphNodeSpawner] Receiver per GraphInputForwarder non trovato (viewportGO e panZoom null).");
                }

                // ------------------------------------------------------------
                // F) Drag nodo manuale (dipendenze)
                // ------------------------------------------------------------
                var drag = go.GetComponent<NPCNodeDrag>();
                if (drag != null)
                {
                    if (panZoom != null) drag.SetDependencies(panZoom, nodesRoot);
                    else Debug.LogWarning("[GraphNodeSpawner] panZoom non assegnato: NPCNodeDrag potrebbe non funzionare correttamente.");
                }

                // ------------------------------------------------------------
                // G) Bind view (testo/visual) se presente
                // ------------------------------------------------------------
                var view = go.GetComponent<NPCNodeView>();
                if (view != null)
                    view.Bind(npcId, core.Name, "Idle");

                // ------------------------------------------------------------
                // H) Posizionamento iniziale
                // ------------------------------------------------------------
                // Per ora: griglia.
                // In futuro: layout per cluster/gruppi/leadership, ecc.
                int row = iNode / safeCols;
                int col = iNode % safeCols;
                rt.anchoredPosition = new Vector2(col * spacingX, -row * spacingY);

                // ------------------------------------------------------------
                // I) Attiva nodo: ora OnEnable può registrarsi correttamente
                // ------------------------------------------------------------
                go.SetActive(true);
                iNode++;
            }

            Debug.Log($"[GraphNodeSpawner] SpawnFromArcontio: spawned {iNode} nodes (Tick={host.TickIndex}).");
        }

        [ContextMenu("Spawn Test")]
        public void SpawnTest()
        {
            if (!CheckEssentials()) return;

            ClearNodes();

            int safeCols = Mathf.Max(1, cols);

            for (int i = 0; i < testCount; i++)
            {
                GameObject go = Instantiate(nodePrefab, nodesRoot);
                go.name = $"NPCAtomNode_Test_{i}";
                go.SetActive(false);

                RectTransform rt = go.GetComponent<RectTransform>();
                if (rt == null)
                {
                    Debug.LogError($"[GraphNodeSpawner] Il prefab '{nodePrefab.name}' non ha RectTransform sul root.");
                    Destroy(go);
                    continue;
                }

                var collision = go.GetComponentInChildren<NPCNodeCollision>(true);
                if (collision != null) collision.SetId(i);

                var uiEvents = go.GetComponentInChildren<NPCNodeUIEvents>(true);
                if (uiEvents != null)
                {
                    uiEvents.SetDependencies(tooltip, contextMenu);
                    uiEvents.SetNodeInfo(i, $"NPC {i}", "Idle");
                }

                var forwarder = go.GetComponent<GraphInputForwarder>();
                if (forwarder != null)
                {
                    var receiver = (viewportGO != null)
                        ? viewportGO
                        : (panZoom != null ? panZoom.gameObject : null);

                    if (receiver != null) forwarder.SetViewportReceiver(receiver);
                }

                var drag = go.GetComponent<NPCNodeDrag>();
                if (drag != null && panZoom != null)
                    drag.SetDependencies(panZoom, nodesRoot);

                var view = go.GetComponent<NPCNodeView>();
                if (view != null)
                    view.Bind(i, $"NPC {i}", "Idle");

                int row = i / safeCols;
                int col = i % safeCols;
                rt.anchoredPosition = new Vector2(col * spacingX, -row * spacingY);

                go.SetActive(true);
            }

            Debug.Log($"[GraphNodeSpawner] SpawnTest: spawned {testCount} nodes.");
        }

        // --------------------------------------------------------------------
        // Helpers privati
        // --------------------------------------------------------------------

        private bool CheckEssentials()
        {
            if (nodesRoot == null)
            {
                Debug.LogError("[GraphNodeSpawner] nodesRoot non assegnato.");
                return false;
            }

            if (nodePrefab == null)
            {
                Debug.LogError("[GraphNodeSpawner] nodePrefab non assegnato.");
                return false;
            }

            return true;
        }

        private void ClearNodes()
        {
            for (int i = nodesRoot.childCount - 1; i >= 0; i--)
                Destroy(nodesRoot.GetChild(i).gameObject);
        }
    }
}
