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
            Debug.Log("[SimulationHost] Awake START");

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

            // NEW (Giorno 5, scelta B):
            // - TokenBus è una pipe separata dal MessageBus
            // - TokenEmissionPipeline NON è uno ISystem: viene chiamata manualmente in StepOneTick.
            //_tokenBus = new TokenBus();
            //_tokenEmission = new TokenEmissionPipeline(contactRadius: 1, topN: 6);

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

            //var memEnc = new MemoryEncodingSystem();
            //memEnc.SetEventsBuffer(_eventBuffer);
            //_scheduler.AddSystem(memEnc);

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

            //_scheduler.AddSystem(new TokenEmissionSystem(contactRadius: 1, topN: 6));
            // NOTA: TokenEmissionSystem non esiste nella scelta B.
            // L'emissione token avviene via _tokenEmission.Emit(...) nel tick.

            // Rules (alto livello)
            _rules.Add(new DebugEventLogRule());
            _rules.Add(new BasicSurvivalRule());

            // Seed iniziale
            EnsureSeeded();
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

            // DEBUG - Genero eventi fittizi per testare creazione di memorie
            // IMPORTANTE: se vuoi che l'evento sia codificato nel tick corrente,
            // deve essere pubblicato PRIMA del DrainTo(...) (cioè prima del punto 3).
            if ((_tickIndex % 50) == 0)
            {
                Debug.Log($"[EventTest] Creato un nuovo evento");

                // Mettiamo un evento in una cella vicino ai primi NPC (es. 0,0)
                _bus.Publish(new PredatorSpottedEvent(
                    spotterNpcId: 1,
                    predatorId: 999,
                    cellX: 0,
                    cellY: 0,
                    distanceCells: 1,
                    spotQuality01: 1f));
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
                int b = _world.Memory[2].Traces.Count;
                Debug.Log($"[MemoryTest] npc1 traces={a} npc2 traces={b}");
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
            _world.Global.FoodStock = 50;

            // Per ora la gestione delle regioni come memoria spaziale è inserita come progetto ma non implementata
            _world.Global.EnableMemorySpatialFusion = false;
            _world.Global.MemoryRegionSizeCells = 4;

            // --- Token params (Giorno 5/6/7) ---
            _world.Global.MaxTokensPerEncounter = 2;
            _world.Global.MaxTokensPerNpcPerDay = 50;       // alto per test (così non “strozza” l’esperimento)
            _world.Global.RepeatShareCooldownTicks = 0;     // per test: vogliamo vedere emissioni ripetute

            // --- Giorno 7: delivery params ---
            // Aumenta un po’ il range così nella coppia col muro lungo il suono “può” arrivare facendo il giro,
            // ma degradato.
            _world.Global.TokenDeliveryMaxRangeCells = 10;

            // LOS: serve per ProximityTalk/TargetedVisit (ottico). AlarmShout invece userà BFS acustico.
            _world.Global.EnableTokenLOS = true;

            // Degrado con distanza (valori “visibili” in log)
            _world.Global.TokenReliabilityFalloffPerCell = 0.06f;
            _world.Global.TokenIntensityFalloffPerCell = 0.04f;

            // ============================================================
            // SCENARIO GUIDATO: 2 coppie separate da muri diversi
            //
            // Coppia A (muro corto, 1 blocco):
            //   NPC_A1 (0,0)  | muro (1,0) | NPC_A2 (2,0)
            //
            // Coppia B (muro lungo, più blocchi):
            //   NPC_B1 (0,5)  | muro lungo su x=1 da y=4..6 | NPC_B2 (2,5)
            //
            // In entrambi i casi i due NPC sono “di fronte” con un muro in mezzo.
            // - ProximityTalk (LOS) -> verrà bloccato sia dal muro corto sia dal muro lungo.
            // - AlarmShout (BFS) -> può “girare attorno”: nel caso muro lungo il giro è più lungo e degrada di più.
            // ============================================================

            // --- Muro corto (1 blocco) ---
            _world.SetOccluder(1, 0, new Occluder
            {
                BlocksVision = true,
                BlocksMovement = true,
                VisionCost = 1.0f
            });

            // --- Muro lungo (3 blocchi in colonna) ---
            // “Parete” verticale che divide x=0.. e x=2.. attorno alla riga y=5
            // Questo aumenta il detour per l’AlarmShout.
            _world.SetOccluder(1, 4, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1.0f });
            _world.SetOccluder(1, 5, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1.0f });
            _world.SetOccluder(1, 6, new Occluder { BlocksVision = true, BlocksMovement = true, VisionCost = 1.0f });

            // --- Creiamo 4 NPC (2 coppie) ---
            // Coppia A
            CreateNpcAt(0, 0, "NPC_A1");
            CreateNpcAt(2, 0, "NPC_A2");

            // Coppia B
            CreateNpcAt(0, 5, "NPC_B1");
            CreateNpcAt(2, 5, "NPC_B2");

            // Helper locale per evitare ripetizioni
            void CreateNpcAt(int x, int y, string name)
            {
                int npcId = _world.CreateNpc(
                    new NpcCore { Name = name, Charisma = 0.4f, Decisiveness = 0.4f, Empathy = 0.4f, Ambition = 0.4f },
                    new Needs { Hunger01 = 0.1f, Fatigue01 = 0.1f, Morale01 = 0.7f, HungerRate = 0.01f, FatigueRate = 0.005f },
                    new Social { LeadershipScore = 0.2f, LoyaltyToLeader01 = 0.5f, JusticePerception01 = 0.5f },
                    x, y
                );

                // Se vuoi, qui puoi differenziare parametri memoria per osservare decay diverso:
                // _world.MemoryParams[npcId] = PersonalityMemoryParams.DefaultNpc();
            }
        }
    }
}
