using System.Collections.Generic;
using UnityEngine;

namespace SocialViewer.UI.Graph
{
    /// <summary>
    /// GraphMagnetSystem (pulito + stabile + gruppi non sovrapposti)
    /// 
    /// SCOPO
    /// - Visualizzare "cluster sociali" attorno a un leader.
    /// - Ogni gruppo ha:
    ///     - leader (id)
    ///     - membri (lista id)
    ///     - coesione 0..1 (influenza su spaziatura e ring base)
    ///     - attrazione 0..1 (forza con cui i membri convergono ai target)
    /// - Supporta più gruppi contemporaneamente.
    /// 
    /// PROBLEMI RISOLTI
    /// 1) Ring "abnormi" con molti membri:
    ///    - NON imponiamo più "tutti sulla stessa circonferenza".
    ///    - Distribuiamo i membri su più shell concentriche (multi-ring placement).
    /// 
    /// 2) Fluttuazione infinita:
    ///    - Snap/Deadzone vicino al target (se vicino e lento -> snap + v=0).
    ///    - Sleep (se forza e velocità sono sotto soglia -> v=0).
    /// 
    /// 3) Overlap tra sfere nel cluster:
    ///    - Hard separation PBD-lite + smorzamento/azzeramento velocità.
    /// 
    /// 4) Anelli di gruppi diversi che si sovrappongono:
    ///    - Group Packing: ogni gruppo è un cerchio (center=leaderPos, radius=outerR).
    ///      Se due cerchi si sovrappongono, spostiamo i LEADER per separarli.
    ///    - È estremamente performante (O(G^2), con G tipicamente piccolo: 10-50).
    /// 
    /// NOTE ARCHITETTURALI
    /// - Questo sistema NON decide la struttura sociale: la riceve dal simulatore (Arcontio).
    /// - L'input esterno dovrebbe chiamare SetMagnet(...) / AddMembers(...) / RemoveMembers(...)
    ///   o, in alternativa, API legacy SetMagnetParams(...) ecc.
    /// 
    /// DIPENDENZE
    /// - NodeRegistry.TryGetNode(int id, out NPCNodeCollision node)
    /// - NodeRegistry.AllNodes
    /// - NPCNodeCollision.Rect (RectTransform) e NPCNodeCollision.radius
    /// - MagnetRingView.Show(RectTransform leaderRect, float radius) + Hide()
    /// - SpatialHashGrid2D (classe griglia già presente nel progetto)
    /// </summary>
    public class GraphMagnetSystem : MonoBehaviour
    {
        // =========================================================
        // References
        // =========================================================

        [Header("References")]
        [Tooltip("Registry dei nodi UI (deve fornire TryGetNode + AllNodes).")]
        [SerializeField] private NodeRegistry registry;

        [Header("Ring View (opzionale)")]
        [Tooltip("Parent sotto cui istanziare gli anelli (tipicamente RingsRoot sotto GraphContent).")]
        [SerializeField] private RectTransform ringsRoot;

        [Tooltip("Prefab del ring (MagnetRingView). Se null, non disegniamo anelli.")]
        [SerializeField] private MagnetRingView ringViewPrefab;

        // =========================================================
        // Ring base + Attrazione (per gruppo)
        // =========================================================

        [Header("Ring base (indicatore coesione)")]
        [Tooltip("Raggio quando coesione è bassa (gruppo più largo).")]
        [SerializeField] private float ringRadiusMax = 340f;

        [Tooltip("Raggio quando coesione è alta (gruppo più stretto).")]
        [SerializeField] private float ringRadiusMin = 160f;

        [Header("Attrazione (base)")]
        [Tooltip("Forza base dell'attrazione verso il target della shell (moltiplicata per Attraction01 del gruppo).")]
        [SerializeField] private float attractionStrength = 900f;

        // =========================================================
        // Multi-Ring placement (shell concentriche)
        // =========================================================

        [Header("Distribuzione su più anelli (multi-ring)")]
        [Tooltip("Se true, i membri sono distribuiti su shell concentriche anziché su un ring unico.")]
        [SerializeField] private bool useMultiRingPlacement = true;

        [Tooltip("Capacità della shell 0: quanti nodi al massimo mettiamo sul ring base prima di passare alla shell successiva.")]
        [SerializeField] private int maxNodesOnBaseRing = 32;

        [Tooltip("Moltiplicatore della spaziatura radiale tra shell. 1.0 ~ minDist (diametro + gap).")]
        [SerializeField] private float ringStepMultiplier = 1.0f;

        [Tooltip("Clamp massimo del raggio del gruppo (0 = nessun clamp). Utile se non vuoi cluster giganteschi.")]
        [SerializeField] private float maxGroupRadiusClamp = 0f;

        // =========================================================
        // Repulsione intra gruppo (sfere)
        // =========================================================

        [Header("Repulsione intra-gruppo")]
        [Tooltip("Forza base della repulsione tra membri vicini (overlap/too close).")]
        [SerializeField] private float repulsionStrength = 14000f;

        [Header("Gap intra-gruppo (spazi visibili)")]
        [Tooltip("Gap quando coesione è bassa (gruppo più aperto).")]
        [SerializeField] private float separationGapLoose = 60f;

        [Tooltip("Gap quando coesione è alta (vuoi comunque spazio visibile).")]
        [SerializeField] private float separationGapTight = 20f;

        [Header("Repulsione vs leader")]
        [Tooltip("Moltiplicatore repulsione quando il nodo si avvicina troppo al leader (anti-collasso nel centro).")]
        [SerializeField] private float leaderRepulsionMultiplier = 1.6f;

        // =========================================================
        // Inerzia / stabilità
        // =========================================================

        [Header("Stabilità movimento")]
        [Tooltip("Smorzamento (drag). Più alto = converge prima ma può attrarre meno se esageri.")]
        [SerializeField] private float damping = 8f;

        [Tooltip("Velocità massima dei nodi (clamp).")]
        [SerializeField] private float maxSpeed = 9000f;

        [Header("Sleep (anti-jitter)")]
        [Tooltip("Se velocità sotto questa soglia e forza sotto sleepForce, fermiamo il nodo (v=0).")]
        [SerializeField] private float sleepSpeed = 6f;

        [Tooltip("Soglia forza per sleep.")]
        [SerializeField] private float sleepForce = 15f;

        [Header("Stabilità dt")]
        [Tooltip("Clamp del deltaTime (evita instabilità con cali FPS).")]
        [SerializeField] private float maxDt = 1f / 30f;

        [Header("Smoothing parametri (coesione/attrazione)")]
        [Tooltip("Tempo di smoothing per convergere ai target dei parametri gruppo.")]
        [SerializeField] private float paramSmoothTime = 0.25f;

        // =========================================================
        // Grid / performance
        // =========================================================

        [Header("Spatial Hash Grid (performance)")]
        [Tooltip("Dimensione cella griglia. Con radius ~9 e gap ~20-60, 130 va bene.")]
        [SerializeField] private float gridCellSize = 130f;

        [Tooltip("Ogni quanti frame ricostruire la griglia. 1 consigliato (specie con hard separation).")]
        [SerializeField] private int rebuildGridEveryNFrames = 1;

        [Header("Repulsione (budget)")]
        [Tooltip("Massimo numero di vicini processati per nodo (limite performance).")]
        [SerializeField] private int maxNeighborsPerNode = 48;

        [Tooltip("Time slicing: 2 = processa repulsione intra-gruppo per metà nodi per frame (alternando).")]
        [SerializeField] private int intraRepulsionStride = 2;

        // =========================================================
        // Snap / stop drifting
        // =========================================================

        [Header("Snap vicino al target (stop drift)")]
        [Tooltip("Se distanza dal target sotto questa soglia e velocità bassa, facciamo snap.")]
        [SerializeField] private float snapDistance = 2.0f;

        [Tooltip("Velocità sotto la quale consentiamo lo snap.")]
        [SerializeField] private float snapSpeed = 8.0f;

        // =========================================================
        // Hard separation (anti-overlap robusto)
        // =========================================================

        [Header("Hard overlap resolution (PBD-lite)")]
        [SerializeField] private bool enableHardSeparation = true;

        [Tooltip("Iterazioni hard. 1 in genere basta; >1 costa di più.")]
        [SerializeField] private int hardSeparationIterations = 1;

        [Tooltip("Correzione massima per frame (clamp) per non teletrasportare troppo.")]
        [SerializeField] private float maxHardCorrectionPerFrame = 35f;

        [Range(0.5f, 1.0f)]
        [Tooltip("Soglia overlap: 0.98 corregge quasi solo se overlap reale, riduce jitter.")]
        [SerializeField] private float hardOverlapThreshold01 = 0.98f;

        [Range(0.0f, 1.0f)]
        [Tooltip("Smorzamento velocità dopo correzione hard. 0.2 = molto stabile.")]
        [SerializeField] private float hardVelocityDamping = 0.2f;

        [Tooltip("Se la correzione è grande, azzeriamo del tutto la velocità per far convergere.")]
        [SerializeField] private float hardZeroVelocityIfCorrectionOver = 6f;

        // =========================================================
        // Ring view: cosa mostra
        // =========================================================

        [Header("Ring view: rappresentazione")]
        [Tooltip("Se true, l'anello mostra il raggio esterno effettivo del cluster (outer radius). Se false, mostra solo ring base (coesione).")]
        [SerializeField] private bool ringShowsOuterRadius = true;

        // =========================================================
        // Group Packing: evita sovrapposizione tra gruppi
        // =========================================================

        [Header("Separazione tra gruppi (group packing)")]
        [Tooltip("Se true, i gruppi vengono separati come cerchi (centro=leader). Evita ring sovrapposti tra gruppi.")]
        [SerializeField] private bool enableGroupPacking = true;

        [Tooltip("Spazio extra tra i cerchi gruppo (padding) in UI units.")]
        [SerializeField] private float groupPadding = 80f;

        [Tooltip("Forza repulsione tra gruppi quando i cerchi si sovrappongono.")]
        [SerializeField] private float groupRepulsionStrength = 2500f;

        [Tooltip("Smorzamento (drag) sulla velocità dei leader dovuta al packing.")]
        [SerializeField] private float groupLeaderDamping = 10f;

        [Tooltip("Velocità massima dei leader dovuta al packing.")]
        [SerializeField] private float groupLeaderMaxSpeed = 2000f;

        // =========================================================
        // Stato interno
        // =========================================================

        // velocità per nodi (membri)
        private readonly Dictionary<int, Vector2> _vel = new();

        // gruppi magnete
        private readonly Dictionary<int, MagnetGroupState> _groups = new();

        // se un nodo sta in più gruppi, lo assegnamo al gruppo "dominante"
        private readonly Dictionary<int, int> _nodeToDominantGroup = new();

        // cache id di tutti i nodi registrati
        private readonly List<int> _allNodeIds = new();

        // ring view per gruppo (se attivo)
        private readonly Dictionary<int, MagnetRingView> _ringByGroup = new();

        // griglia spaziale per query vicini
        private readonly SpatialHashGrid2D _grid = new SpatialHashGrid2D();

        // velocità dei leader dovuta al "group packing" (se attivo)
        private readonly Dictionary<int, Vector2> _leaderPackVel = new();

        private int _frameCounter;

        // angolo deterministico per ID (stabile nel tempo)
        private const float GOLDEN_RATIO_CONJ = 0.61803398875f;

        // =========================================================
        // API (moderna)
        // =========================================================

        /// <summary>
        /// Set completo di un gruppo magnete:
        /// - magnetKey: id del gruppo/cluster (può essere id gruppo Arcontio)
        /// - leaderId: id nodo leader
        /// - members: lista membri (può NON includere il leader; lo aggiungiamo comunque)
        /// - cohesion01: 0..1
        /// - attraction01: 0..1
        /// </summary>
        public void SetMagnet(int magnetKey, int leaderId, IReadOnlyList<int> members, float cohesion01, float attraction01)
        {
            if (registry == null) registry = NodeRegistry.Instance;
            if (registry == null)
            {
                Debug.LogError("[GraphMagnetSystem] NodeRegistry non trovato: impossibile applicare magneti.");
                return;
            }

            if (!_groups.TryGetValue(magnetKey, out var g))
            {
                g = new MagnetGroupState(magnetKey);
                _groups.Add(magnetKey, g);
            }

            g.LeaderId = leaderId;
            g.CohesionTarget01 = Mathf.Clamp01(cohesion01);
            g.AttractionTarget01 = Mathf.Clamp01(attraction01);

            g.SetMembers(members, leaderId);

            EnsureVelEntriesForGroup(g);
            EnsureRingViewForGroup(magnetKey);

            if (!_leaderPackVel.ContainsKey(leaderId))
                _leaderPackVel[leaderId] = Vector2.zero;
        }

        /// <summary>
        /// Aggiorna solo parametri gruppo.
        /// </summary>
        public void UpdateParams(int magnetKey, float cohesion01, float attraction01)
        {
            if (_groups.TryGetValue(magnetKey, out var g))
            {
                g.CohesionTarget01 = Mathf.Clamp01(cohesion01);
                g.AttractionTarget01 = Mathf.Clamp01(attraction01);
            }
        }

        /// <summary>
        /// Cambia il leader del gruppo. Manteniamo membri, ma assicuriamo che il leader sia incluso.
        /// </summary>
        public void ChangeLeader(int magnetKey, int leaderId)
        {
            if (_groups.TryGetValue(magnetKey, out var g))
            {
                g.LeaderId = leaderId;
                g.EnsureLeaderInMembers();
                g.RebuildStableOrder();

                if (!_leaderPackVel.ContainsKey(leaderId))
                    _leaderPackVel[leaderId] = Vector2.zero;
            }
        }

        /// <summary>
        /// Aggiunge membri a un gruppo esistente.
        /// </summary>
        public void AddMembers(int magnetKey, IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0) return;
            if (!_groups.TryGetValue(magnetKey, out var g)) return;

            for (int i = 0; i < ids.Count; i++)
                g.MemberSet.Add(ids[i]);

            g.EnsureLeaderInMembers();
            g.RebuildStableOrder();
            EnsureVelEntriesForGroup(g);
        }

        /// <summary>
        /// Rimuove membri da un gruppo esistente.
        /// NOTA: non rimuoviamo mai il leader (se presente nella lista, lo ignoriamo).
        /// </summary>
        public void RemoveMembers(int magnetKey, IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0) return;
            if (!_groups.TryGetValue(magnetKey, out var g)) return;

            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                if (id == g.LeaderId) continue;
                g.MemberSet.Remove(id);
            }

            g.EnsureLeaderInMembers();
            g.RebuildStableOrder();
        }

        public void ClearGroup(int magnetKey)
        {
            _groups.Remove(magnetKey);

            if (_ringByGroup.TryGetValue(magnetKey, out var ring) && ring != null)
                Destroy(ring.gameObject);

            _ringByGroup.Remove(magnetKey);
        }

        // =========================================================
        // API legacy (nomi AL PLURALE come da tuo standard)
        // =========================================================

        public void SetMagnetParams(int magnetKey, float cohesion01, float attraction01)
            => UpdateParams(magnetKey, cohesion01, attraction01);

        public void SetMagnetLeader(int magnetKey, int leaderId)
            => ChangeLeader(magnetKey, leaderId);

        public void ClearMagnet(int magnetKey)
            => ClearGroup(magnetKey);

        public void ClearAllMagnets()
        {
            var keys = new List<int>(_groups.Keys);
            for (int i = 0; i < keys.Count; i++)
                ClearGroup(keys[i]);
        }

        // =========================================================
        // Unity lifecycle
        // =========================================================

        private void Awake()
        {
            if (registry == null) registry = NodeRegistry.Instance;

            // Sanity per evitare valori non validi
            rebuildGridEveryNFrames = Mathf.Max(1, rebuildGridEveryNFrames);
            intraRepulsionStride = Mathf.Max(1, intraRepulsionStride);
            maxNeighborsPerNode = Mathf.Max(4, maxNeighborsPerNode);
            hardSeparationIterations = Mathf.Max(1, hardSeparationIterations);
            maxNodesOnBaseRing = Mathf.Max(4, maxNodesOnBaseRing);

            _grid.SetCellSize(gridCellSize);

            if (registry == null)
                Debug.LogError("[GraphMagnetSystem] NodeRegistry non trovato/assegnato.");
        }

        private void Update()
        {
            if (registry == null) return;

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            // clamp dt per stabilità
            dt = Mathf.Min(dt, maxDt);

            _frameCounter++;

            BuildAllNodeIdCache();
            if (_allNodeIds.Count == 0)
            {
                HideAllRings();
                return;
            }

            // smoothing parametri di gruppo
            foreach (var kv in _groups)
                kv.Value.TickSmoothing(paramSmoothTime, dt);

            // se un nodo sta in più gruppi, scegliamo quello dominante
            BuildDominantGroupMap();

            RebuildGridIfNeeded();

            // 1) Separazione tra gruppi (sposta LEADER)
            ApplyGroupPacking(dt);

            // 2) Aggiorna anelli (dopo packing, così si disegnano dove stanno i leader)
            UpdateAllRings();

            // 3) Forze sui membri (attrazione/repulsione)
            ApplyGroupForces(dt);

            // 4) Correzione hard overlap
            if (enableHardSeparation)
                ResolveOverlapsHard();
        }

        // =========================================================
        // Cache nodi / velocità
        // =========================================================

        private void BuildAllNodeIdCache()
        {
            _allNodeIds.Clear();

            var all = registry.AllNodes;
            if (all == null || all.Count == 0) return;

            for (int i = 0; i < all.Count; i++)
            {
                var n = all[i];
                if (n == null) continue;

                _allNodeIds.Add(n.Id);

                // se manca entry velocità, la creiamo
                if (!_vel.ContainsKey(n.Id))
                    _vel[n.Id] = Vector2.zero;
            }
        }

        // =========================================================
        // Dominant group map
        // =========================================================

        private void BuildDominantGroupMap()
        {
            _nodeToDominantGroup.Clear();
            if (_groups.Count == 0) return;

            for (int i = 0; i < _allNodeIds.Count; i++)
            {
                int nodeId = _allNodeIds[i];

                float bestScore = float.NegativeInfinity;
                int bestGroup = int.MinValue;
                bool found = false;

                foreach (var kv in _groups)
                {
                    var g = kv.Value;

                    if (!g.MemberSet.Contains(nodeId))
                        continue;

                    // score semplice ma stabile: attrazione pesata da coesione
                    float score = g.Attraction01 * (0.5f + 0.5f * g.Cohesion01);

                    if (!found || score > bestScore)
                    {
                        found = true;
                        bestScore = score;
                        bestGroup = g.MagnetKey;
                    }
                }

                if (found)
                    _nodeToDominantGroup[nodeId] = bestGroup;
            }
        }

        // =========================================================
        // Grid rebuild
        // =========================================================

        private void RebuildGridIfNeeded()
        {
            if ((_frameCounter % rebuildGridEveryNFrames) != 0)
                return;

            _grid.SetCellSize(gridCellSize);
            _grid.Clear();

            for (int i = 0; i < _allNodeIds.Count; i++)
            {
                int id = _allNodeIds[i];

                if (!registry.TryGetNode(id, out var node) || node == null || node.Rect == null)
                    continue;

                _grid.Add(id, node.Rect.anchoredPosition);
            }
        }

        // =========================================================
        // Group packing (separazione tra cluster)
        // =========================================================

        private void ApplyGroupPacking(float dt)
        {
            if (!enableGroupPacking) return;
            if (_groups.Count <= 1) return;

            // Costruiamo una lista di chiavi (G tipicamente piccolo: 10-50)
            var groupKeys = new List<int>(_groups.Keys);

            // Preinizializziamo velocità leader se mancano
            for (int i = 0; i < groupKeys.Count; i++)
            {
                var g = _groups[groupKeys[i]];
                if (!_leaderPackVel.ContainsKey(g.LeaderId))
                    _leaderPackVel[g.LeaderId] = Vector2.zero;
            }

            // Per ogni gruppo A calcoliamo forza repulsiva verso gli altri gruppi B
            for (int i = 0; i < groupKeys.Count; i++)
            {
                var gA = _groups[groupKeys[i]];

                if (!registry.TryGetNode(gA.LeaderId, out var leaderA) || leaderA == null || leaderA.Rect == null)
                    continue;

                Vector2 posA = leaderA.Rect.anchoredPosition;
                float rA = ComputeGroupOuterRadius(gA, leaderA) + groupPadding;

                Vector2 forceA = Vector2.zero;

                for (int j = 0; j < groupKeys.Count; j++)
                {
                    if (j == i) continue;

                    var gB = _groups[groupKeys[j]];

                    if (!registry.TryGetNode(gB.LeaderId, out var leaderB) || leaderB == null || leaderB.Rect == null)
                        continue;

                    Vector2 posB = leaderB.Rect.anchoredPosition;
                    float rB = ComputeGroupOuterRadius(gB, leaderB) + groupPadding;

                    Vector2 d = posA - posB;
                    float dist = d.magnitude;

                    if (dist < 0.0001f)
                    {
                        // se coincidono quasi, scegliamo una direzione arbitraria
                        d = Vector2.right;
                        dist = 0.0001f;
                    }

                    float minDist = rA + rB;

                    // Se i cerchi si sovrappongono, spingiamo A via da B
                    if (dist < minDist)
                    {
                        float t = 1f - (dist / minDist);   // 0..1
                        Vector2 dir = d / dist;

                        // t^2 = spinta più forte quando overlap è grande
                        float strength = groupRepulsionStrength * (t * t);

                        forceA += dir * strength;
                    }
                }

                // Integra velocità leader (packing)
                int leaderId = gA.LeaderId;
                Vector2 v = _leaderPackVel[leaderId];

                v += forceA * dt;

                // clamp velocità
                float sp = v.magnitude;
                if (sp > groupLeaderMaxSpeed)
                    v = v / sp * groupLeaderMaxSpeed;

                // damping esponenziale
                float drag = Mathf.Exp(-groupLeaderDamping * dt);
                v *= drag;

                // applica movimento leader
                leaderA.Rect.anchoredPosition = posA + v * dt;

                _leaderPackVel[leaderId] = v;
            }
        }

        // =========================================================
        // Forze sui membri: attrazione + repulsione
        // =========================================================

        private void ApplyGroupForces(float dt)
        {
            if (_groups.Count == 0) return;

            foreach (var kv in _groups)
            {
                MagnetGroupState g = kv.Value;

                if (!registry.TryGetNode(g.LeaderId, out var leaderNode) || leaderNode == null || leaderNode.Rect == null)
                    continue;

                Vector2 leaderPos = leaderNode.Rect.anchoredPosition;

                // Gap in funzione coesione
                float separationGap = Mathf.Lerp(separationGapLoose, separationGapTight, g.Cohesion01);

                // Ring base in funzione coesione
                float ringBaseR = ComputeRingRadius(g.Cohesion01);

                // minDist center-to-center tra membri (approssimiamo con radius leader)
                float approxRadius = leaderNode.radius;
                float minDist = (approxRadius * 2f) + separationGap;

                // step radiale tra shell
                float step = minDist * Mathf.Max(0.5f, ringStepMultiplier);

                // attrazione del gruppo
                float attraction = attractionStrength * g.Attraction01;

                // Leader: il solver interno NON lo muove (si muove solo col packing)
                _vel[g.LeaderId] = Vector2.zero;

                // Iteriamo membri non-leader in ordine stabile
                int cap = Mathf.Max(4, maxNodesOnBaseRing);

                for (int idx = 0; idx < g.NonLeaderSorted.Count; idx++)
                {
                    int idA = g.NonLeaderSorted[idx];

                    // Solo gruppo dominante
                    if (_nodeToDominantGroup.TryGetValue(idA, out int dom) && dom != g.MagnetKey)
                        continue;

                    if (!registry.TryGetNode(idA, out var nodeA) || nodeA == null || nodeA.Rect == null)
                        continue;

                    RectTransform rectA = nodeA.Rect;
                    Vector2 posA = rectA.anchoredPosition;

                    // ===========================
                    // MULTI-RING placement
                    // ===========================
                    int shell = useMultiRingPlacement ? (idx / cap) : 0;

                    float shellR = ringBaseR + shell * step;

                    if (maxGroupRadiusClamp > 0f)
                        shellR = Mathf.Min(shellR, maxGroupRadiusClamp);

                    // Angolo deterministico per ID
                    float t01 = (idA * GOLDEN_RATIO_CONJ) % 1f;
                    float angle = t01 * Mathf.PI * 2f;

                    Vector2 target = leaderPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * shellR;
                    Vector2 toTarget = target - posA;

                    // ===========================
                    // SNAP / DEADZONE
                    // ===========================
                    Vector2 vA = _vel[idA];
                    if (toTarget.sqrMagnitude <= snapDistance * snapDistance && vA.sqrMagnitude <= snapSpeed * snapSpeed)
                    {
                        rectA.anchoredPosition = target;
                        _vel[idA] = Vector2.zero;
                        continue;
                    }

                    Vector2 force = Vector2.zero;

                    // (1) Attrazione
                    force += toTarget * (attraction * Mathf.Lerp(0.35f, 1.0f, g.Cohesion01));

                    // (2) Repulsione vs leader: evita che qualcuno vada "dentro" il centro
                    {
                        float minDistLeader = nodeA.radius + leaderNode.radius + separationGap;

                        Vector2 dL = posA - leaderPos;
                        float distL = dL.magnitude;

                        if (distL < 0.0001f)
                        {
                            dL = Vector2.right;
                            distL = 0.0001f;
                        }

                        if (distL < minDistLeader)
                        {
                            float tL = 1f - (distL / minDistLeader);
                            Vector2 dirL = dL / distL;

                            float strengthL = repulsionStrength * leaderRepulsionMultiplier * (tL * tL * tL);
                            force += dirL * strengthL;
                        }
                    }

                    // (3) Repulsione intra-gruppo: time-sliced per performance
                    bool doThisNodeRepulsion = ((idA + _frameCounter) % intraRepulsionStride) == 0;

                    if (doThisNodeRepulsion)
                    {
                        List<int> neighbors = _grid.QueryNeighbors(posA);

                        float radiusA = nodeA.radius;
                        int processed = 0;

                        for (int n = 0; n < neighbors.Count; n++)
                        {
                            int idB = neighbors[n];
                            if (idB == idA) continue;

                            // stesso gruppo dominante
                            if (!_nodeToDominantGroup.TryGetValue(idB, out int domB) || domB != g.MagnetKey)
                                continue;

                            // non processiamo leader qui (già gestito in repulsione vs leader)
                            if (idB == g.LeaderId) continue;

                            if (!registry.TryGetNode(idB, out var nodeB) || nodeB == null || nodeB.Rect == null)
                                continue;

                            Vector2 posB = nodeB.Rect.anchoredPosition;

                            Vector2 d = posA - posB;
                            float dist = d.magnitude;

                            if (dist < 0.0001f)
                            {
                                d = Vector2.right;
                                dist = 0.0001f;
                            }

                            float minDistAB = radiusA + nodeB.radius + separationGap;

                            if (dist < minDistAB)
                            {
                                float t = 1f - (dist / minDistAB);
                                Vector2 dir = d / dist;

                                float strength = repulsionStrength * (t * t * t);

                                // coesione alta => repulsione leggermente più "morbida"
                                strength *= Mathf.Lerp(1.0f, 0.85f, g.Cohesion01);

                                force += dir * strength;
                            }

                            processed++;
                            if (processed >= maxNeighborsPerNode)
                                break;
                        }
                    }

                    // (4) Integrazione stabile
                    IntegrateVelocityAndMove(idA, rectA, posA, force, dt);
                }
            }
        }

        // =========================================================
        // Hard separation (PBD-lite): impedisce overlap residuo
        // =========================================================

        private void ResolveOverlapsHard()
        {
            float maxCorr = Mathf.Max(1f, maxHardCorrectionPerFrame);
            float thr = Mathf.Clamp(hardOverlapThreshold01, 0.5f, 1.0f);

            // alterniamo leggermente per non creare "bias" direzionali
            bool parity = (_frameCounter & 1) == 0;

            for (int iter = 0; iter < hardSeparationIterations; iter++)
            {
                foreach (var kv in _groups)
                {
                    var g = kv.Value;

                    if (!registry.TryGetNode(g.LeaderId, out var leaderNode) || leaderNode == null || leaderNode.Rect == null)
                        continue;

                    float separationGap = Mathf.Lerp(separationGapLoose, separationGapTight, g.Cohesion01);

                    // Correggiamo solo non-leader (leader ancorato)
                    for (int idx = 0; idx < g.NonLeaderSorted.Count; idx++)
                    {
                        int idA = g.NonLeaderSorted[idx];

                        // gruppo dominante
                        if (_nodeToDominantGroup.TryGetValue(idA, out int dom) && dom != g.MagnetKey)
                            continue;

                        if (!registry.TryGetNode(idA, out var nodeA) || nodeA == null || nodeA.Rect == null)
                            continue;

                        Vector2 posA = nodeA.Rect.anchoredPosition;
                        float radiusA = nodeA.radius;

                        Vector2 corrAcc = Vector2.zero;

                        List<int> neighbors = _grid.QueryNeighbors(posA);

                        int processed = 0;

                        for (int i = 0; i < neighbors.Count; i++)
                        {
                            int idB = neighbors[i];
                            if (idB == idA) continue;

                            // leader mai corretto qui
                            if (idB == g.LeaderId) continue;

                            if (!_nodeToDominantGroup.TryGetValue(idB, out int domB) || domB != g.MagnetKey)
                                continue;

                            if (!registry.TryGetNode(idB, out var nodeB) || nodeB == null || nodeB.Rect == null)
                                continue;

                            Vector2 posB = nodeB.Rect.anchoredPosition;

                            Vector2 d = posA - posB;
                            float dist = d.magnitude;

                            if (dist < 0.0001f)
                            {
                                d = Vector2.right;
                                dist = 0.0001f;
                            }

                            float minDist = radiusA + nodeB.radius + separationGap;

                            // correggiamo solo se overlap reale/serio
                            if (dist < minDist * thr)
                            {
                                float penetration = (minDist - dist);
                                Vector2 dir = d / dist;

                                float share = 0.5f; // correzione parziale per stabilità

                                if (parity) corrAcc += dir * (penetration * share);
                                else corrAcc += dir * (penetration * (share * 0.9f));
                            }

                            processed++;
                            if (processed >= maxNeighborsPerNode)
                                break;
                        }

                        float mag = corrAcc.magnitude;
                        if (mag > 0.0001f)
                        {
                            Vector2 corr = corrAcc;

                            // clamp della correzione per frame
                            if (mag > maxCorr)
                                corr = corr / mag * maxCorr;

                            nodeA.Rect.anchoredPosition = posA + corr;

                            // smorzamento/azzeramento velocità per evitare jitter perpetuo
                            if (_vel.TryGetValue(idA, out var v))
                            {
                                if (mag >= hardZeroVelocityIfCorrectionOver)
                                    _vel[idA] = Vector2.zero;
                                else
                                    _vel[idA] = v * hardVelocityDamping;
                            }
                        }
                    }
                }
            }
        }

        // =========================================================
        // Integrazione stabile
        // =========================================================

        private void IntegrateVelocityAndMove(int nodeId, RectTransform rect, Vector2 pos, Vector2 force, float dt)
        {
            Vector2 v = _vel[nodeId];

            // forza -> velocità
            v += force * dt;

            // clamp velocità
            float sp = v.magnitude;
            if (sp > maxSpeed)
                v = v / sp * maxSpeed;

            // drag esponenziale (stabile a dt variabile)
            float drag = Mathf.Exp(-damping * dt);
            v *= drag;

            // sleep: se quasi fermo e quasi senza forze, spegniamo la velocità
            if (v.sqrMagnitude < sleepSpeed * sleepSpeed && force.sqrMagnitude < sleepForce * sleepForce)
                v = Vector2.zero;

            rect.anchoredPosition = pos + v * dt;
            _vel[nodeId] = v;
        }

        // =========================================================
        // Ring view
        // =========================================================

        private void EnsureRingViewForGroup(int magnetKey)
        {
            if (ringViewPrefab == null || ringsRoot == null) return;

            if (_ringByGroup.TryGetValue(magnetKey, out var existing) && existing != null)
                return;

            MagnetRingView ring = Instantiate(ringViewPrefab, ringsRoot);
            ring.gameObject.name = $"MagnetRing_{magnetKey}";
            _ringByGroup[magnetKey] = ring;
        }

        private void UpdateAllRings()
        {
            if (ringViewPrefab == null || ringsRoot == null) return;

            foreach (var kv in _groups)
            {
                var g = kv.Value;

                if (!_ringByGroup.TryGetValue(g.MagnetKey, out var ring) || ring == null)
                    continue;

                if (!registry.TryGetNode(g.LeaderId, out var leaderNode) || leaderNode == null || leaderNode.Rect == null)
                {
                    ring.Hide();
                    continue;
                }

                float ringBaseR = ComputeRingRadius(g.Cohesion01);
                float showR = ringBaseR;

                if (ringShowsOuterRadius)
                {
                    float outerR = ComputeGroupOuterRadius(g, leaderNode);
                    showR = outerR;
                }

                ring.Show(leaderNode.Rect, showR);
            }
        }

        private void HideAllRings()
        {
            foreach (var kv in _ringByGroup)
                if (kv.Value != null) kv.Value.Hide();
        }

        // =========================================================
        // Helpers: calcoli raggio + velocità
        // =========================================================

        private void EnsureVelEntriesForGroup(MagnetGroupState g)
        {
            if (!_vel.ContainsKey(g.LeaderId))
                _vel[g.LeaderId] = Vector2.zero;

            for (int i = 0; i < g.NonLeaderSorted.Count; i++)
            {
                int id = g.NonLeaderSorted[i];
                if (!_vel.ContainsKey(id))
                    _vel[id] = Vector2.zero;
            }
        }

        private float ComputeRingRadius(float cohesion01)
        {
            float c = Mathf.Clamp01(cohesion01);
            return Mathf.Lerp(ringRadiusMax, ringRadiusMin, c);
        }

        /// <summary>
        /// Calcola l'outer radius effettivo del cluster, coerente con multi-ring placement.
        /// Questo valore è usato sia per:
        /// - ring view (se ringShowsOuterRadius = true)
        /// - group packing (se enableGroupPacking = true)
        /// </summary>
        private float ComputeGroupOuterRadius(MagnetGroupState g, NPCNodeCollision leaderNode)
        {
            float ringBaseR = ComputeRingRadius(g.Cohesion01);

            float separationGap = Mathf.Lerp(separationGapLoose, separationGapTight, g.Cohesion01);

            float approxRadius = leaderNode.radius;
            float minDist = (approxRadius * 2f) + separationGap;

            float step = minDist * Mathf.Max(0.5f, ringStepMultiplier);

            int cap = Mathf.Max(4, maxNodesOnBaseRing);
            int members = Mathf.Max(1, g.NonLeaderSorted.Count);

            int shells = useMultiRingPlacement ? Mathf.CeilToInt(members / (float)cap) : 1;
            float outerR = ringBaseR + Mathf.Max(0, shells - 1) * step;

            if (maxGroupRadiusClamp > 0f)
                outerR = Mathf.Min(outerR, maxGroupRadiusClamp);

            return outerR;
        }

        // =========================================================
        // Stato gruppo (ordine stabile)
        // =========================================================

        private class MagnetGroupState
        {
            public int MagnetKey { get; }
            public int LeaderId;

            // membership O(1)
            public readonly HashSet<int> MemberSet = new HashSet<int>();

            // lista non-leader ordinata (shell assignment stabile)
            public readonly List<int> NonLeaderSorted = new List<int>(256);

            // target parametri
            public float CohesionTarget01;
            public float AttractionTarget01;

            // parametri smoothed (quelli usati dal solver)
            public float Cohesion01;
            public float Attraction01;

            // velocity per SmoothDamp
            private float _cohesionVel;
            private float _attractionVel;

            public MagnetGroupState(int key)
            {
                MagnetKey = key;

                CohesionTarget01 = 1f;
                AttractionTarget01 = 1f;

                Cohesion01 = 1f;
                Attraction01 = 1f;
            }

            public void SetMembers(IReadOnlyList<int> members, int leaderId)
            {
                LeaderId = leaderId;

                MemberSet.Clear();
                NonLeaderSorted.Clear();

                if (members != null)
                {
                    for (int i = 0; i < members.Count; i++)
                        MemberSet.Add(members[i]);
                }

                EnsureLeaderInMembers();
                RebuildStableOrder();
            }

            public void EnsureLeaderInMembers()
            {
                MemberSet.Add(LeaderId);
            }

            public void RebuildStableOrder()
            {
                NonLeaderSorted.Clear();

                foreach (int id in MemberSet)
                {
                    if (id == LeaderId) continue;
                    NonLeaderSorted.Add(id);
                }

                // ordine stabile tra run
                NonLeaderSorted.Sort();
            }

            public void TickSmoothing(float smoothTime, float dt)
            {
                float st = Mathf.Max(0.0001f, smoothTime);

                Cohesion01 = Mathf.SmoothDamp(Cohesion01, CohesionTarget01, ref _cohesionVel, st, Mathf.Infinity, dt);
                Attraction01 = Mathf.SmoothDamp(Attraction01, AttractionTarget01, ref _attractionVel, st, Mathf.Infinity, dt);

                Cohesion01 = Mathf.Clamp01(Cohesion01);
                Attraction01 = Mathf.Clamp01(Attraction01);
            }
        }
    }
}
