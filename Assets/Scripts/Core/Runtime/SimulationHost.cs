// Assets/Scripts/Core/Runtime/SimulationHost.cs
using Arcontio.Core.Diagnostics;
using Arcontio.Core.Logging;
using System.Collections.Generic;
using System.Security.Cryptography;
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
            Day8_ObjectPerception = 8,
            Day9_NeedsOwnership = 9,
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
            // Anti-duplicazione: se esiste già un host, distruggo questo.
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Inizializzo il log personalizzato
            ArcontioLogger.InitFromResources(
               gameParamsPathNoExt: "Config/game_params",
               localizationPathNoExt: "Config/localization_logs"
           );
            ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "Core"),
                new LogBlock(LogLevel.Info, "log.core.persistent_path")
                    .AddField("path", Application.persistentDataPath)
            );

            // Creo la finestra per visualizzare il log personalizzato a schermo
            if (FindObjectOfType<Arcontio.View.ArcontioLogOverlay>() == null)
            {
                new GameObject("ArcontioLogOverlay")
                    .AddComponent<Arcontio.View.ArcontioLogOverlay>();
            }

            // Inizializzo una sola volta il mondo
            // Leggo da file game_params.json i dati di simulazione, che finiscono in simParams.
            // Con quello creo l'istanza di world
            var simParams = Arcontio.Core.Config.SimulationParamsLoader.LoadFromResources("Arcontio/Config/game_params");
            _world = new World(new WorldConfig(simParams));

            _bus = new MessageBus();
            _scheduler = new Scheduler();
            _telemetry = new Telemetry();

            // Carichiamo definizioni oggetti da JSON (Resources/Config/object_defs.json).
            ObjectDatabaseLoader.LoadIntoWorld(_world);

            // Carica parametri fame/sonno da JSON
            NeedsConfigLoader.LoadIntoWorld(_world);

            // Decisione eat/sleep/steal (scenario guidato)
            //_scheduler.AddSystem(new NeedsDecisionSystem());
            
            // Systems (basso livello)
            _scheduler.AddSystem(new NeedsDecaySystem());

            // Systems (basso livello)
            // - Perception oggetti: genera eventi ObjectSpottedEvent
            _scheduler.AddSystem(new ObjectPerceptionSystem());
            //_scheduler.AddSystem(new TheftSuspicionSystem());

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
            _rules.Add(new NeedsDecisionRule(decisionEveryTicks: 25));

            // Seed iniziale
            EnsureSeeded();

            ArcontioLogger.Debug(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "Test"),
                new LogBlock(LogLevel.Debug, "log.test.scenario")
                    .AddField("scenario", debugScenario)
            );
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
            // Rende il tick disponibile globalmente per logging in comandi ecc.
            TickContext.BeginTick(_tickIndex);

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
            
            // Pulse "clock" per rules: DEVE essere regolare e frequente.
            _bus.Publish(new TickPulseEvent(_tickIndex));

            // Se vuoi anche un log visivo ogni 50 tick, tienilo separato:
            if ((_tickIndex % 50) == 0)
                ArcontioLogger.Trace(
                    new LogContext(tick: _tickIndex, channel: "TickPulse"),
                    new LogBlock(LogLevel.Trace, "log.tickpulse.tick")
                );

            // ============================================================
            // TEST STIMULI (Day6 / Day7 / Day8)
            // Inseriamo SOLO ciò che serve a generare segnali chiari.
            // ============================================================

            // Day6: generiamo un evento "oggettivo" che finisce in memoria, poi token, poi assimilation.
            if (debugScenario == DebugScenario.Day6_Assimilation && ((_tickIndex % 50) == 0))
            {
                ArcontioLogger.Debug(
                    new LogContext(tick: _tickIndex, channel: "T6"),
                    new LogBlock(LogLevel.Debug, "log.t6.predator_injected")
                );
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

                ArcontioLogger.Debug(
                    new LogContext(tick: _tickIndex, channel: "T7"),
                    new LogBlock(LogLevel.Debug, "log.t7.alarmshout.published")
                        .AddField("routes", "1->2, 3->4")
                );
            }

            // Day8: non serve iniettare eventi a mano: ObjectPerceptionSystem li produce da solo.
            // Qui lasciamo che i log siano:
            // - Telemetry.ObjectPerception.SpottedEvents
            // - (se vuoi) un tuo log aggiuntivo ogni 50 tick
            if (debugScenario == DebugScenario.Day8_ObjectPerception && ((_tickIndex % 50) == 0))
            {
                ArcontioLogger.Debug(
                    new LogContext(tick: _tickIndex, channel: "T8"),
                    new LogBlock(LogLevel.Debug, "log.t8.info")
                        .AddField("objects", _world.Objects.Count)
                        .AddField("vision", _world.Global.NpcVisionRangeCells)
                );
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

            // Post-command drain + memory encoding
            // Perché: i comandi pubblicano eventi FACT (es: FoodStolenWorldEvent).
            // Se non li draini qui, li encodi solo nel tick successivo.
            _eventBuffer.Clear();
            _bus.DrainTo(_eventBuffer);

            if (_eventBuffer.Count > 0)
            {
                // encodiamo memorie anche per gli eventi post-comando
                _memoryEncoding.Update(_world, tick, _bus, _telemetry);

                // opzionale: se vuoi che le rules li vedano nello stesso tick, ripubblichi:
                for (int i = 0; i < _eventBuffer.Count; i++)
                    _bus.Publish(_eventBuffer[i]);
            }

            // DEBUG
            // Stampa quanti ricordi hanno due NPC specifici (utile per vedere decadimento e merge).
            if (_tickIndex % 20 == 0)
            {
                int a = _world.Memory[1].Traces.Count;
                int b = _world.Memory.Count >= 2 ? _world.Memory[2].Traces.Count : 0;
                ArcontioLogger.Debug(
                    new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "MemoryTest"),
                    new LogBlock(LogLevel.Debug, "log.memorytest.traces")
                        .AddField("npc1", a)
                        .AddField("npc2", b)
                );
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
                ArcontioLogger.Debug(
                    new LogContext(tick: _tickIndex, channel: "Arcontio"),
                    new LogBlock(LogLevel.Debug, "log.arcontio.tick_summary")
                        .AddField("food", _world.Global.FoodStock)
                        .AddField("npc", _world.NpcCore.Count)
                );
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
                
                case DebugScenario.Day9_NeedsOwnership:
                    Seed_Day9();
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
            ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T6"),
                new LogBlock(LogLevel.Info, "log.seed.name")
                    .AddField("seed", "Day6_Assimilation")
            );

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

            ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T6"),
                new LogBlock(LogLevel.Info, "log.seed.expectation")
                    .AddField("expectation", "tick%50 PredatorSpotted -> memory -> token -> assimilation (heard trace on listener).")
            );

            int CreateNpcAt(int x, int y, string name)
            {
                return _world.CreateNpc(
                    new NpcCore { Name = name, Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                    new Needs { Hunger01 = 0.1f, Fatigue01 = 0.1f, Morale01 = 0.7f},
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
            ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T7"),
                new LogBlock(LogLevel.Info, "log.seed.name")
                    .AddField("seed", "Day7_Delivery")
            );

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

            ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T7"),
                new LogBlock(LogLevel.Info, "log.seed.expectation")
                    .AddField("expectation", "every 50 ticks inject AlarmShout (1->2 and 3->4). Delivery logs show different dist/deg for short vs long wall.")
            );

            int CreateNpcAt(int x, int y, string name)
            {
                return _world.CreateNpc(
                    new NpcCore { Name = name, Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                    new Needs { Hunger01 = 0.1f, Fatigue01 = 0.1f, Morale01 = 0.7f},
                    new Social { LeadershipScore = 0.2f, LoyaltyToLeader01 = 0.5f, JusticePerception01 = 0.5f },
                    x, y
                );
            }
        }

        // ============================================================
        // Object perception test (cone FOV + ObjectSpottedEvent -> memory)
        // ============================================================
        private void Seed_Day8()
        {
            ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T8"),
                new LogBlock(LogLevel.Info, "log.seed.name")
                    .AddField("seed", "Day8_ObjectPerception")
            );

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
                new Needs { Hunger01 = 0.1f, Fatigue01 = 0.1f, Morale01 = 0.7f},
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

            ArcontioLogger.Debug(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T8"),
                new LogBlock(LogLevel.Debug, "log.t8.info0")
                    .AddField("objects", _world.Objects.Count)
                    .AddField("vision", _world.Global.NpcVisionRangeCells)
                    .AddField("cone", _world.Global.NpcVisionConeHalfWidthPerStep.ToString("0.00"))
            );
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
                ArcontioLogger.Warn(
                    new LogContext(tick: tick.Index, channel: "T8"),
                    new LogBlock(LogLevel.Warn, "log.t8.snap.missing_pos")
                        .AddField("npc", "npc1")
                );
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
            ArcontioLogger.Trace(
                new LogContext(tick: tick.Index, channel: "T8"),
                new LogBlock(LogLevel.Trace, "log.t8.snap.details")
                    .AddField("npc", "npc1")
                    .AddField("pos", $"({np.X},{np.Y})")
                    .AddField("facing", facing)
                    .AddField("vision", vision)
                    .AddField("cone", cone.ToString("0.00"))
                    .AddField("seesCount", seen.Count)
                    .AddField("list", list)
            );
        }

        private void Seed_Day9()
        {
            ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "T9"),
                new LogBlock(LogLevel.Info, "log.seed.name")
                    .AddField("seed", "Day9_Needs_Food_Beds_Theft")
            );

            _world.Global.FoodStock = 0;

            // --- Day9: needs config già caricata da JSON ---
            // Se vuoi forzare per test (override):
            // _world.Global.Needs = NeedsConfig.Default();

            // Vision per far sì che NPC1 "veda e ricordi" (Day8/9)
            _world.Global.NpcVisionRangeCells = 6;
            _world.Global.NpcVisionUseCone = true;
            _world.Global.NpcVisionConeSlope = 1.0f; // cono ampio (45° circa su grid)

            // Disattivo token per non inquinare log del day9
            _world.Global.MaxTokensPerEncounter = 0;
            _world.Global.MaxTokensPerNpcPerDay = 0;
            _world.Global.RepeatShareCooldownTicks = 0;

            // ============================================================
            // SCENARIO:
            // - NPC1 (id=1) e NPC2 (id=2)
            // - 2 letti (1 community, 1 owned da NPC2)
            // - 1 stock cibo libero visibile
            // - 4 cibo privato di NPC2
            // - 1 stock cibo libero nascosto da occluder (non visibile a NPC1)
            //
            // Aspettative:
            // - NPC1 vede e ricorda i letti (nel cono).
            // - NPC2 consuma prima cibo privato.
            // - NPC1 consuma stock libero; finito, può rubare da NPC2 se lawfulness basso
            // - NPC1 sceglie letto community se libero.
            // ============================================================

            // Occluder per “nascondere” stock libero #2 a NPC1 (blocca LOS percezione)
            // Nota: questo funziona solo se in ObjectPerceptionSystem hai check LOS sugli occluder.
            //_world.SetOccluder(2, 1, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1f });

            int wall = _world.CreateObject(defId: "wall_stone", x: 22, y: 23);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
             wall = _world.CreateObject(defId: "wall_stone", x: 22, y: 22);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
             wall = _world.CreateObject(defId: "wall_stone", x: 22, y: 21);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
             wall = _world.CreateObject(defId: "wall_stone", x: 22, y: 20);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
            wall = _world.CreateObject(defId: "wall_stone", x: 22, y: 19);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
            wall = _world.CreateObject(defId: "wall_stone", x: 22, y: 18);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
            wall = _world.CreateObject(defId: "wall_stone", x: 22, y: 17);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
            wall = _world.CreateObject(defId: "wall_stone", x: 23, y: 17);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
            wall = _world.CreateObject(defId: "wall_stone", x: 24, y: 17);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
            wall = _world.CreateObject(defId: "wall_stone", x: 25, y: 17);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));
            wall = _world.CreateObject(defId: "wall_stone", x: 26, y: 17);
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                                 new LogBlock(LogLevel.Debug, "object.spawn")
                                 .AddField("obj", "wall_stone")
                                 .AddField("id", wall));


            //_world.SetOccluder(2, 2, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1f });
            //_world.SetOccluder(2, 3, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1f });

            // NPC1: basso rispetto legge (ruberà “facile”)
            int npc1 = _world.CreateNpc(
                new NpcCore { Name = "NPC1", Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                new Needs { Hunger01 = 0.85f, Fatigue01 = 0.85f, Morale01 = 0.7f, IsHungry = false },
                new Social { LeadershipScore = 0.2f, LoyaltyToLeader01 = 0.5f, JusticePerception01 = 0.20f },
                x: 20, y: 20
            );

            // NPC2: alto rispetto legge (non ruba; ha cibo privato)
            int npc2 = _world.CreateNpc(
                new NpcCore { Name = "NPC2", Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                new Needs { Hunger01 = 0.80f, Fatigue01 = 0.40f, Morale01 = 0.7f, IsHungry = false },
                new Social { LeadershipScore = 0.2f, LoyaltyToLeader01 = 0.5f, JusticePerception01 = 0.90f },
                x: 20, y: 22
            );

            // NPC3: alto rispetto legge (non ruba; ha cibo privato)
            int npc3 = _world.CreateNpc(
                new NpcCore { Name = "NPC3", Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                new Needs { Hunger01 = 0.80f, Fatigue01 = 0.40f, Morale01 = 0.7f, IsHungry = false },
                new Social { LeadershipScore = 0.2f, LoyaltyToLeader01 = 0.5f, JusticePerception01 = 0.90f },
                x: 28, y: 19
            );
            // Facing: NPC1 guarda verso "su" dove mettiamo letti/food
            _world.SetFacing(npc1, CardinalDirection.North);
            _world.SetFacing(npc2, CardinalDirection.North);
            _world.SetFacing(npc3, CardinalDirection.West);

            // --- Oggetti: letti ---
            int bedCommunity = _world.CreateObject(defId: "bed_wood_poor", x: 20, y: 23, ownerKind: OwnerKind.Community, ownerId: 0);
            _world.ObjectUse[bedCommunity] = ObjectUseState.Free();

            ArcontioLogger.Debug(new LogContext(0, "T9"),
               new LogBlock(LogLevel.Debug, "object.spawn")
                  .AddField("obj", "bed_wood_poor")
                  .AddField("units", 1));

            int bedNpc2 = _world.CreateObject(defId: "bed_wood_good", x: 21, y: 23, ownerKind: OwnerKind.Npc, ownerId: npc2);
            _world.ObjectUse[bedNpc2] = ObjectUseState.Free();

            ArcontioLogger.Debug(new LogContext(0, "T9"),
               new LogBlock(LogLevel.Debug, "object.spawn")
                  .AddField("obj", "bed_wood_good")
                  .AddField("units", 1));

            // --- Cibo: stock libero visibile (davanti a NPC1) ---
            int foodFreeVisible = _world.CreateObject(defId: "food_stock", x: 20, y: 21, ownerKind: OwnerKind.Community, ownerId: 0);
            _world.FoodStocks[foodFreeVisible] = new FoodStockComponent { Units = 5, OwnerKind = OwnerKind.Community, OwnerId = 0 };

            ArcontioLogger.Debug(new LogContext(0, "T9"),
               new LogBlock(LogLevel.Debug, "object.spawn")
                  .AddField("obj", "food_stock visible")
                  .AddField("units", 5));

            // --- Cibo: stock libero nascosto (dietro muro/occluder) ---
            int foodFreeHidden = _world.CreateObject(defId: "food_stock", x: 23, y: 22, ownerKind: OwnerKind.Community, ownerId: 0);
            _world.FoodStocks[foodFreeHidden] = new FoodStockComponent { Units = 3, OwnerKind = OwnerKind.Community, OwnerId = 0 };
            
            ArcontioLogger.Debug(new LogContext(0, "T9"),
                new LogBlock(LogLevel.Debug, "object.spawn")
                   .AddField("obj", "food_stock hidden")
                   .AddField("units", 3));

            // --- Cibo privato NPC2 ---
            _world.NpcPrivateFood[npc2] = 4;

            ArcontioLogger.Info(new LogContext(0, "T9"),
                new LogBlock(LogLevel.Info, "log.t9.seed.setup_done"));

            ArcontioLogger.Info(new LogContext(0, "T9"),
                new LogBlock(LogLevel.Info, "log.t9.seed.npc1")
                    .AddField("id", npc1)
                    .AddField("pos", "(0,0)")
                    .AddField("law", _world.Social[npc1].JusticePerception01.ToString("0.00"))
                    .AddField("hunger", _world.Needs[npc1].Hunger01.ToString("0.00"))
                    .AddField("fatigue", _world.Needs[npc1].Fatigue01.ToString("0.00"))
            );

            ArcontioLogger.Info(new LogContext(0, "T9"),
                new LogBlock(LogLevel.Info, "log.t9.seed.npc2")
                    .AddField("id", npc2)
                    .AddField("pos", "(0,2)")
                    .AddField("law", _world.Social[npc2].JusticePerception01.ToString("0.00"))
                    .AddField("privateFood", _world.NpcPrivateFood[npc2])
                    .AddField("hunger", _world.Needs[npc2].Hunger01.ToString("0.00"))
            );

            ArcontioLogger.Info(new LogContext(0, "T9"),
                new LogBlock(LogLevel.Info, "log.t9.seed.objects")
                    .AddField("bedCommunity", bedCommunity)
                    .AddField("bedNpc2", bedNpc2)
                    .AddField("foodFreeVisible", foodFreeVisible)
                    .AddField("foodFreeHidden", foodFreeHidden)
            );
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
        private void LateUpdate()
        {
            // flush “soft” a fine frame (evita I/O per log write singolo)
            ArcontioLogger.Flush();
        }

        private void OnApplicationQuit()
        {
            ArcontioLogger.Shutdown();
        }
    }
}
