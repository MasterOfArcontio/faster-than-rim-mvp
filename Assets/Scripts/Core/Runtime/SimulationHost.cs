// Assets/Scripts/Core/Runtime/SimulationHost.cs
using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// SimulationHost: orchestra il ciclo del simulatore Arcontio.
    ///
    /// Nota architetturale:
    /// - Questo oggetto vive su ArcontioRuntime (DontDestroyOnLoad),
    ///   quindi deve essere "singleton" e non deve duplicarsi tra scene.
    /// - Le scene (AtomView, MapGrid, ecc.) sono SOLO viste: leggono World e inviano comandi,
    ///   ma non creano un altro SimulationHost.
    /// </summary>
    public sealed class SimulationHost : MonoBehaviour
    {
        [Header("Tick")]
        [SerializeField] private float tickDeltaTime = 1f; // tempo simulato per tick (es. 1 = 1 minuto)
        [SerializeField] private int ticksPerSecond = 10;  // velocità simulazione (tick/secondo reali)

        [Header("Debug Scenarios")]
        [SerializeField] private DebugScenario debugScenario = DebugScenario.Day7_Delivery;

        private enum DebugScenario
        {
            Day6_Assimilation = 6,
            Day7_Delivery = 7,
            Day8_ObjectPerception = 8
        }

        // Day8: log sintetico per tick (solo per questo scenario)
        [SerializeField] private int day8LogEveryTicks = 10;


        // Core state
        private World _world;
        private MessageBus _bus;
        private Scheduler _scheduler;
        private Telemetry _telemetry;

        private TokenBus _tokenBusOut;                  // “messaggi pronunciati” (non ancora consegnati)
        private TokenBus _tokenBusIn;                   // “messaggi ricevuti” (pronti per assimilation)
        private TokenDeliveryPipeline _tokenDelivery;   // decide chi li sente davvero
        private TokenEmissionPipeline _tokenEmission;   // Decide cosa dire
        private TokenAssimilationPipeline _tokenAssim;  // Decide cosa entra in testa
        // MemoryTrace
        //     |
        //     V
        // TokenEmissionPipeline
        //     |
        //     V
        // TokenBusOut(parlato)
        //     |
        //     V
        // TokenDeliveryPipeline
        //     |
        //     V
        // TokenBusIn(sentito)
        //     |
        //     V
        // TokenAssimilationPipeline
        //     |
        //     V
        // MemoryTrace / Rumor / Belief

        private MemoryEncodingSystem _memoryEncoding;

        // Working buffers (evitano allocazioni ogni tick)
        private readonly List<ISystem> _toRun = new();
        private readonly List<IRule> _rules = new();
        private readonly List<ICommand> _commands = new();

        // buffer debug (drain token bus)
        private readonly List<TokenEnvelope> _tokenBuffer = new(256);

        private readonly List<ISimEvent> _eventBuffer = new();

        // Contatore del tick (long)
        private long _tickIndex;

        // Accumulatore di tempo reale per eseguire tick discreti
        private float _accum;

        // Singleton / accesso per le viste
        public static SimulationHost Instance { get; private set; }
        public World World => _world;
        public long TickIndex => _tickIndex;

        // Flag per evitare seeding multipli (in caso di scene reload accidentali)
        private bool _seeded;

        private void Awake()
        {
            Debug.Log(Application.persistentDataPath);

            // Anti-duplicazione: se esiste già un host, distruggo questo.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Inizializzo una sola volta
            _world = new World();
            _bus = new MessageBus();
            _scheduler = new Scheduler();
            _telemetry = new Telemetry();

            // NEW (Giorno 8):
            // Carichiamo definizioni oggetti da JSON (Resources/Config/object_defs.json).
            ObjectDatabaseLoader.LoadIntoWorld(_world);

            // Systems (basso livello)
            // - Perception oggetti: genera eventi ObjectSpottedEvent
            _scheduler.AddSystem(new ObjectPerceptionSystem());

            // Giorno 7: separiamo token "pronunciati" da token "arrivati"
            _tokenBusOut = new TokenBus();
            _tokenBusIn = new TokenBus();

            _tokenEmission = new TokenEmissionPipeline(contactRadius: 2, topN: 6);

            // NEW Giorno 7:
            // - applica range / LOS / falloff
            // - trasferisce Out -> In
            _tokenDelivery = new TokenDeliveryPipeline();

            // NEW: pipeline di assimilazione (giorno 6)
            _tokenAssim = new TokenAssimilationPipeline();

            // Metto qui e non nello scheduler l'encoding della memoria per problemi di sincronia
            // (deve vedere ESATTAMENTE gli eventi del tick drainati nel buffer)
            _memoryEncoding = new MemoryEncodingSystem();
            _memoryEncoding.SetEventsBuffer(_eventBuffer);

            // Systems (basso livello)
            // _scheduler.AddSystem(new NeedsSystem());
            // (commentato da te: ok, finché stai facendo scenario guidato su memoria/comms)

            // Poi decay (maintenance)
            // NEW: decadimento memoria (non fa nulla finché lo store è vuoto)
            _scheduler.AddSystem(new MemoryDecaySystem());

            // Rules (alto livello)
            _rules.Add(new DebugEventLogRule());
            _rules.Add(new BasicSurvivalRule());

            // Seed iniziale
            EnsureSeeded();

            Debug.Log($"[TEST] Scenario={debugScenario}");
        }

        private void Update()
        {
            // Converte frame time reale in tick discreti
            float dt = Time.unscaledDeltaTime;
            _accum += dt;

            float tickInterval = 1f / Mathf.Max(1, ticksPerSecond);

            while (_accum >= tickInterval)
            {
                _accum -= tickInterval;
                StepOneTick();
            }
        }

        private void StepOneTick()
        {
            var tick = new Tick(_tickIndex, tickDeltaTime);

            // 1) Scheduler decide quali systems girano in questo tick
            _scheduler.GetSystemsToRun(_tickIndex, _toRun);

            // 2) Esegue systems (possono mutare World e pubblicare eventi)
            // Nota: questi systems pubblicano eventi "di mondo" e/o tecnici nel MessageBus.
            for (int i = 0; i < _toRun.Count; i++)
                _toRun[i].Update(_world, tick, _bus, _telemetry);

            // 2.5) Debug pulse (prima del drain)
            // Serve solo per vedere che il loop tick sta avanzando.
            // Non deve generare memorie (non è un IWorldEvent).
            if ((_tickIndex % 50) == 0)
                _bus.Publish(new TickPulseEvent(_tickIndex));

            // ============================================================
            // TEST STIMULI (Day6 / Day7 / Day8)
            // Inseriamo SOLO ciò che serve a generare segnali chiari.
            // ============================================================

            // Day6: generiamo un evento "oggettivo" che finisce in memoria, poi token, poi assimilation.
            if (debugScenario == DebugScenario.Day6_Assimilation && ((_tickIndex % 50) == 0))
            {
                Debug.Log($"[T6][Event] PredatorSpotted injected at tick={_tickIndex}");
                _bus.Publish(new PredatorSpottedEvent(
                    spotterNpcId: 1,
                    predatorId: 999,
                    cellX: 0,
                    cellY: 0,
                    distanceCells: 1,
                    spotQuality01: 1f));
            }

            // Day7: iniettiamo DIRETTAMENTE un AlarmShout in TokenBusOut
            // (così testiamo Delivery BFS anche se le emission rule di default parlano “ProximityTalk”).
            if (debugScenario == DebugScenario.Day7_Delivery && ((_tickIndex % 50) == 0))
            {
                var shout = new SymbolicToken(
                    type: TokenType.PredatorAlert,
                    subjectId: 999,
                    intensity01: 1.0f,
                    reliability01: 1.0f,
                    chainDepth: 0,
                    hasCell: true,
                    cellX: 0,
                    cellY: 0);

                // Coppia A: 1 -> 2
                _tokenBusOut.Publish(new TokenEnvelope(
                    speakerId: 1,
                    listenerId: 2,
                    channel: TokenChannel.AlarmShout,
                    tickIndex: _tickIndex,
                    token: shout));

                // Coppia B: 3 -> 4
                _tokenBusOut.Publish(new TokenEnvelope(
                    speakerId: 3,
                    listenerId: 4,
                    channel: TokenChannel.AlarmShout,
                    tickIndex: _tickIndex,
                    token: shout));

                Debug.Log($"[T7][Inject] AlarmShout published (1->2 and 3->4) tick={_tickIndex}");
            }

            // Day8: non serve iniettare eventi a mano: ObjectPerceptionSystem li produce da solo.
            // Qui lasciamo che i log siano:
            // - Telemetry.ObjectPerception.SpottedEvents
            // - (se vuoi) un tuo log aggiuntivo ogni 50 tick
            if (debugScenario == DebugScenario.Day8_ObjectPerception && ((_tickIndex % 50) == 0))
            {
                Debug.Log($"[T8][Info] tick={_tickIndex} objects={_world.Objects.Count} vision={_world.Global.NpcVisionRangeCells}");
            }

            // 3) Drain eventi in buffer (così possiamo farci sopra encoding memoria)
            // Dopo questo punto:
            // - _eventBuffer contiene TUTTI gli eventi pubblicati finora nel tick
            // - il bus resta vuoto
            _eventBuffer.Clear();
            _bus.DrainTo(_eventBuffer);

            // 3.1) Memory encoding (evento -> trace)
            // Ora il buffer è pieno: codifichiamo memorie per gli NPC testimoni.
            // Nota: _memoryEncoding NON sta nello scheduler (per evitare problemi di sincronia).
            _memoryEncoding.Update(_world, tick, _bus, _telemetry);

            // 3.15 Token emission (trace -> token) su pipe separata
            // Trasformiamo alcune trace importanti in TokenEnvelope e le mettiamo nel TokenBus.
            // Nota: questo NON tocca il MessageBus e NON influenza direttamente le Rule.
            //
            // Nota test:
            // - Day7 inietta anche token manualmente su _tokenBusOut (AlarmShout) per testare BFS delivery.
            _tokenEmission.Emit(_world, tick, _tokenBusOut, _telemetry);

            // NEW Giorno 7:
            // - Delivery: Out -> In (range / LOS / falloff)
            // - Questo è il punto che evita "telepatia":
            //   il token può NON arrivare, o arrivare degradato.
            _tokenDelivery.Deliver(_world, tick, _tokenBusOut, _tokenBusIn, _telemetry);

            // Assimilation legge SOLO IN (arrivati)
            _tokenAssim.Assimilate(_world, tick, _tokenBusIn, _tokenBuffer, _telemetry);

            // 3.2) Ripubblichiamo gli eventi, così le rules li vedono come prima
            // In questo modo manteniamo l'architettura originale:
            // - i Systems pubblicano eventi
            // - le Rule reagiscono a eventi e generano comandi
            for (int i = 0; i < _eventBuffer.Count; i++)
                _bus.Publish(_eventBuffer[i]);

            // 4) Consuma eventi e lascia reagire le rules (producono comandi)
            _commands.Clear();

            while (_bus.TryDequeue(out var e))
            {
                for (int r = 0; r < _rules.Count; r++)
                    _rules[r].Handle(_world, e, _commands, _telemetry);
            }

            // 4) Esegue comandi (mutano World)
            for (int c = 0; c < _commands.Count; c++)
                _commands[c].Execute(_world, _bus);

            // DEBUG
            // Stampa quanti ricordi hanno due NPC specifici (utile per vedere decadimento e merge).
            if (_tickIndex % 20 == 0)
            {
                int a = _world.Memory[1].Traces.Count;
                int b = _world.Memory.Count >= 2 ? _world.Memory[2].Traces.Count : 0;
                Debug.Log($"[MemoryTest] npc1 traces={a} npc2 traces={b}");
            }

            // ============================================================
            // Day8: log sintetico per tick (solo qui)
            // - Non vogliamo spam in Day6/Day7.
            // - In Day8 vogliamo vedere "quali oggetti" vengono visti.
            // ============================================================
            if (debugScenario == DebugScenario.Day8_ObjectPerception)
            {
                if (day8LogEveryTicks <= 0) day8LogEveryTicks = 10;

                if ((_tickIndex % day8LogEveryTicks) == 0)
                {
                    LogDay8Snapshot(tick);
                }
            }

            _tickIndex++;

            // Debug: verifica che l'host resti vivo cambiando scena
            if (_tickIndex % 50 == 0)
            {
                Debug.Log($"[Arcontio] Tick={_tickIndex} Food={_world.Global.FoodStock} NPC={_world.NpcCore.Count}");
                _telemetry.DumpToConsole();
            }
        }

        /// <summary>
        /// Garantisce che il mondo sia seedato una sola volta.
        /// Serve per evitare rigenerazioni accidentali.
        /// </summary>
        private void EnsureSeeded()
        {
            if (_seeded) return;
            _seeded = true;

            SeedTestWorld();
        }

        private void SeedTestWorld()
        {
            // Seed multiplo: scegliamo UNO scenario per volta,
            // così i log sono chiari e non si sovrappongono.
            switch (debugScenario)
            {
                case DebugScenario.Day6_Assimilation:
                    Seed_Day6();
                    break;

                case DebugScenario.Day7_Delivery:
                    Seed_Day7();
                    break;

                case DebugScenario.Day8_ObjectPerception:
                    Seed_Day8();
                    break;

                default:
                    Seed_Day7();
                    break;
            }
        }

        // ============================================================
        // DAY 6: Assimilation test (token -> trace heard/rumor)
        // ============================================================
        private void Seed_Day6()
        {
            Debug.Log("[T6][Seed] Day6_Assimilation");

            _world.Global.FoodStock = 50;

            // Per ora la gestione delle regioni come memoria spaziale è inserita come progetto ma non implementata
            _world.Global.EnableMemorySpatialFusion = false;
            _world.Global.MemoryRegionSizeCells = 4;

            // Token budget alto per test
            _world.Global.MaxTokensPerEncounter = 2;
            _world.Global.MaxTokensPerNpcPerDay = 50;
            _world.Global.RepeatShareCooldownTicks = 0;

            // Delivery “neutro”: nessun muro, LOS off (così non blocchi per caso)
            _world.Global.TokenDeliveryMaxRangeCells = 10;
            _world.Global.EnableTokenLOS = false;

            // Falloff quasi nullo per rendere i valori leggibili e stabili
            _world.Global.TokenReliabilityFalloffPerCell = 0.00f;
            _world.Global.TokenIntensityFalloffPerCell = 0.00f;

            // 2 NPC vicini così emission trova contatto facilmente
            int a = CreateNpcAt(0, 0, "NPC_T6_A");
            int b = CreateNpcAt(1, 0, "NPC_T6_B");

            _world.SetFacing(a, CardinalDirection.East);
            _world.SetFacing(b, CardinalDirection.West);

            Debug.Log("[T6][Seed] Expectation: on tick%50 PredatorSpotted -> memory -> token -> assimilation (heard trace on listener).");

            int CreateNpcAt(int x, int y, string name)
            {
                return _world.CreateNpc(
                    new NpcCore { Name = name, Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                    new Needs { Hunger01 = 0.1f, Fatigue01 = 0.1f, Morale01 = 0.7f, HungerRate = 0.01f, FatigueRate = 0.005f },
                    new Social { LeadershipScore = 0.2f, LoyaltyToLeader01 = 0.5f, JusticePerception01 = 0.5f },
                    x, y
                );
            }
        }

        // ============================================================
        // DAY 7: Delivery test (range/LOS + BFS shout detour)
        // ============================================================
        private void Seed_Day7()
        {
            Debug.Log("[T7][Seed] Day7_Delivery");

            _world.Global.FoodStock = 50;

            // Token params (Giorno 5/6/7)
            _world.Global.MaxTokensPerEncounter = 2;
            _world.Global.MaxTokensPerNpcPerDay = 50;
            _world.Global.RepeatShareCooldownTicks = 0;

            // Delivery params (Giorno 7)
            _world.Global.TokenDeliveryMaxRangeCells = 10;
            _world.Global.EnableTokenLOS = true;
            _world.Global.TokenReliabilityFalloffPerCell = 0.06f;
            _world.Global.TokenIntensityFalloffPerCell = 0.04f;

            // NEW (Giorno 8): range visivo base (non è il focus qui, ma serve comunque a non avere valori 0)
            _world.Global.NpcVisionRangeCells = 6;

            // ============================================================
            // SCENARIO GUIDATO: 2 coppie separate da muri diversi
            //
            // Coppia A (muro corto, 1 blocco):
            //   NPC_A1 (0,0)  | muro (1,0) | NPC_A2 (2,0)
            //
            // Coppia B (muro lungo, più blocchi):
            //   NPC_B1 (0,5)  | muro lungo su x=1 da y=4..6 | NPC_B2 (2,5)
            //
            // In entrambi i casi:
            // - ProximityTalk (LOS) -> bloccato dal muro pieno.
            // - AlarmShout (BFS) -> aggira muri; muro lungo = detour maggiore => più degrado.
            // ============================================================

            // Muro corto
            _world.SetOccluder(1, 0, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1.0f });

            // Muro lungo
            _world.SetOccluder(1, 4, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1.0f });
            _world.SetOccluder(1, 5, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1.0f });
            _world.SetOccluder(1, 6, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1.0f });

            int a1 = CreateNpcAt(0, 0, "NPC_A1");
            int a2 = CreateNpcAt(2, 0, "NPC_A2");
            int b1 = CreateNpcAt(0, 5, "NPC_B1");
            int b2 = CreateNpcAt(2, 5, "NPC_B2");

            _world.SetFacing(a1, CardinalDirection.East);
            _world.SetFacing(a2, CardinalDirection.West);
            _world.SetFacing(b1, CardinalDirection.East);
            _world.SetFacing(b2, CardinalDirection.West);

            Debug.Log("[T7][Seed] Expectation: every 50 ticks we inject AlarmShout (1->2 and 3->4). Delivery logs show different dist/deg for short vs long wall.");

            int CreateNpcAt(int x, int y, string name)
            {
                return _world.CreateNpc(
                    new NpcCore { Name = name, Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                    new Needs { Hunger01 = 0.1f, Fatigue01 = 0.1f, Morale01 = 0.7f, HungerRate = 0.01f, FatigueRate = 0.005f },
                    new Social { LeadershipScore = 0.2f, LoyaltyToLeader01 = 0.5f, JusticePerception01 = 0.5f },
                    x, y
                );
            }
        }

        // ============================================================
        // DAY 8: Object perception test (cone FOV + ObjectSpottedEvent -> memory)
        // ============================================================
        private void Seed_Day8()
        {
            Debug.Log("[T8][Seed] Day8_ObjectPerception");

            _world.Global.FoodStock = 50;

            // Vision range per test
            _world.Global.NpcVisionRangeCells = 6;
            _world.Global.NpcVisionConeHalfWidthPerStep = 1.0f; // CONO attivo


            // Token systems possono restare attivi, ma qui il focus è:
            // ObjectPerceptionSystem -> ObjectSpottedEvent -> ObjectSpottedMemoryRule
            _world.Global.MaxTokensPerEncounter = 0; // opzionale: “spengo” token per non inquinare log
            _world.Global.MaxTokensPerNpcPerDay = 0;
            _world.Global.RepeatShareCooldownTicks = 0;

            // 1 NPC che guarda a Est
            int npc = _world.CreateNpc(
                new NpcCore { Name = "NPC_T8", Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                new Needs { Hunger01 = 0.1f, Fatigue01 = 0.1f, Morale01 = 0.7f, HungerRate = 0.01f, FatigueRate = 0.005f },
                new Social { LeadershipScore = 0.2f, LoyaltyToLeader01 = 0.5f, JusticePerception01 = 0.5f },
                0, 0
            );

            _world.SetFacing(npc, CardinalDirection.East);

            // Piazziamo oggetti nel cono:
            // - davanti (2,0) -> deve essere visto
            // - diagonale davanti (2,1) -> deve essere visto (cono)
            // - dietro ( -2,0 ) -> NON deve essere visto
            _world.CreateObject(defId: "bed_wood_poor", x: 2, y: 0, ownerKind: OwnerKind.Npc, ownerId: npc);
            _world.CreateObject(defId: "workbench_basic", x: 2, y: 1, ownerKind: OwnerKind.Community, ownerId: 0);
            _world.CreateObject(defId: "chair_basic", x: -2, y: 0, ownerKind: OwnerKind.Community, ownerId: 0);

            Debug.Log($"[T8][Info] tick=0 objects={_world.Objects.Count} vision={_world.Global.NpcVisionRangeCells} cone={_world.Global.NpcVisionConeHalfWidthPerStep:0.00}");
        }

        /// <summary>
        /// LogDay8Snapshot:
        /// Log sintetico solo nel seed Day8:
        /// - elenca gli oggetti visibili per NPC_1 in questo momento (cone + range)
        /// - ti permette di validare rapidamente “bed/workbench sì, chair dietro no”.
        ///
        /// Nota:
        /// qui NON usiamo eventi: è una "sonda" di debug.
        /// Serve a capire se la geometria di FOV è corretta senza rincorrere 100 eventi.
        /// </summary>
        private void LogDay8Snapshot(Tick tick)
        {
            // Assunzione test: esiste almeno npcId=1
            int npcId = 1;
            if (!_world.GridPos.TryGetValue(npcId, out var np))
            {
                Debug.Log($"[T8][Snap] tick={tick.Index} npc1 missing pos");
                return;
            }

            if (!_world.NpcFacing.TryGetValue(npcId, out var facing))
                facing = CardinalDirection.North;

            int vision = _world.Global.NpcVisionRangeCells <= 0 ? 6 : _world.Global.NpcVisionRangeCells;
            float cone = _world.Global.NpcVisionConeHalfWidthPerStep;
            if (cone < 0f) cone = 0f;

            // raccogli defId in set (no duplicati)
            var seen = new HashSet<string>();

            foreach (var kv in _world.Objects)
            {
                var obj = kv.Value;
                if (obj == null) continue;

                int dist = Mathf.Abs(obj.CellX - np.X) + Mathf.Abs(obj.CellY - np.Y);
                if (dist > vision) continue;

                // Riusa la stessa logica del sistema (replicata qui per debug)
                if (!IsInCone_Debug(np.X, np.Y, facing, obj.CellX, obj.CellY, cone))
                    continue;

                seen.Add(obj.DefId);
            }

            string list = (seen.Count == 0) ? "<none>" : string.Join(", ", seen);
            Debug.Log($"[T8][Snap] tick={tick.Index} npc1=({np.X},{np.Y}) facing={facing} vision={vision} cone={cone:0.00} sees={seen.Count} [{list}]");
        }

        // Copia ridotta della logica "IsInCone" per debug snapshot.
        // (Tenuta qui per non dipendere da metodi privati del system)
        private static bool IsInCone_Debug(int sx, int sy, CardinalDirection facing, int tx, int ty, float coneHalfWidthPerStep)
        {
            int dx = tx - sx;
            int dy = ty - sy;

            int forward, side;

            switch (facing)
            {
                case CardinalDirection.North: forward = dy; side = dx; break;
                case CardinalDirection.South: forward = -dy; side = -dx; break;
                case CardinalDirection.East: forward = dx; side = -dy; break;
                case CardinalDirection.West: forward = -dx; side = dy; break;
                default: return false;
            }

            if (forward <= 0) return false;

            int absSide = side < 0 ? -side : side;
            return absSide <= Mathf.FloorToInt(forward * coneHalfWidthPerStep + 0.0001f);
        }

    }
}
