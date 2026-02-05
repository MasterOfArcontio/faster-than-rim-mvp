using System;
using System.Collections.Generic;
using UnityEngine;


namespace Arcontio.Core
{
    /// <summary>
    /// TokenEmissionPipeline (Giorno 5):
    /// Trasforma MemoryTrace -> TokenEnvelope quando due NPC sono in contatto.
    ///
    /// Perché non è un ISystem:
    /// - ISystem.Update(...) non riceve TokenBus.
    /// - Con pipe separate (B), l'orchestrazione deve essere fatta dal SimulationHost.
    /// </summary>
    public sealed class TokenEmissionPipeline
    {
        private readonly List<ITokenEmissionRule> _rules = new();

        // Buffers riusabili (zero alloc per tick)
        private readonly List<int> _npcIds = new(2048);
        private readonly List<MemoryTrace> _topTraces = new(32);

        // Rate limit state
        private readonly Dictionary<int, int> _tokensEmittedToday = new();   // speaker -> count
        private readonly Dictionary<ShareKey, long> _lastShareTick = new();  // cooldown

        // Parametri "base"
        private readonly int _contactRadius;
        private readonly int _topN;

        public TokenEmissionPipeline(int contactRadius = 1, int topN = 6)
        {
            _contactRadius = Math.Max(1, contactRadius);
            _topN = Math.Max(1, topN);

            // Rules minime (roadmap)
            _rules.Add(new PredatorAlertEmissionRule());
            _rules.Add(new HelpRequestEmissionRule());
        }

        /// <summary>
        /// Emette token sul TokenBus, in base alle memorie attuali.
        /// </summary>
        public void Emit(World world, Tick tick, TokenBus tokenBus, Telemetry telemetry)
        {
            UnityEngine.Debug.Log($"[TokenEmit] tick={tick.Index} npcCount={world.NpcCore.Count} tokenOutCount(before)={tokenBus.Count}");

            // Parametri da World (configurabili)
            int maxPerEncounter = world.Global.MaxTokensPerEncounter;
            if (maxPerEncounter <= 0) maxPerEncounter = 1;

            int maxPerDay = world.Global.MaxTokensPerNpcPerDay;
            if (maxPerDay <= 0) maxPerDay = 4;

            int cooldownTicks = world.Global.RepeatShareCooldownTicks;
            if (cooldownTicks < 0) cooldownTicks = 0;

            // Reset "giornaliero" semplice:
            // Assunzione: 1 tick = 1 minuto => 1440 tick = 1 giorno.
            // È un placeholder che sostituirai con un calendario vero.
            if (tick.Index % 1440 == 0)
                _tokensEmittedToday.Clear();

            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            int envelopesEmitted = 0;

            // Contatti per prossimità (O(N^2) per ora: ok per 400 NPC)
            for (int i = 0; i < _npcIds.Count; i++)
            {
                int a = _npcIds[i];
                if (!world.GridPos.TryGetValue(a, out var pa)) continue;

                for (int j = i + 1; j < _npcIds.Count; j++)
                {
                    int b = _npcIds[j];
                    if (!world.GridPos.TryGetValue(b, out var pb)) continue;

                    int dist = Manhattan(pa.X, pa.Y, pb.X, pb.Y);
                    if (dist > _contactRadius) continue;

                    // incontro: A->B e B->A (simmetrico)
                    envelopesEmitted += EmitForPair(world, tick, tokenBus, telemetry, a, b, maxPerEncounter, maxPerDay, cooldownTicks);
                    envelopesEmitted += EmitForPair(world, tick, tokenBus, telemetry, b, a, maxPerEncounter, maxPerDay, cooldownTicks);
                }
            }

            telemetry.Counter("TokenEmissionPipeline.EnvelopesEmitted", envelopesEmitted);
        }

        private int EmitForPair(
                   World world,
                   Tick tick,
                   TokenBus tokenBus,
                   Telemetry telemetry,
                   int speakerId,
                   int listenerId,
                   int maxPerEncounter,
                   int maxPerDay,
                   int cooldownTicks)
        {
            if (!world.Memory.TryGetValue(speakerId, out var store) || store == null)
                return 0;

            _tokensEmittedToday.TryGetValue(speakerId, out int emittedToday);
            if (emittedToday >= maxPerDay)
                return 0;

            // scegliamo le top trace dello speaker
            store.GetTopTraces(_topN, _topTraces);

            UnityEngine.Debug.Log($"[TokenEmit] speaker={speakerId} listener={listenerId} topTraces={_topTraces.Count}");
            if (_topTraces.Count > 0)
                UnityEngine.Debug.Log($"[TokenEmit] top0={_topTraces[0]}");

            int emittedThisEncounter = 0;

            for (int t = 0; t < _topTraces.Count; t++)
            {
                if (emittedThisEncounter >= maxPerEncounter)
                    break;

                var trace = _topTraces[t];

                // prova rules
                for (int r = 0; r < _rules.Count; r++)
                {
                    var rule = _rules[r];
                    if (!rule.Matches(trace))
                        continue;

                    // cooldown "stessa informazione alla stessa persona"
                    var key = new ShareKey(speakerId, listenerId, trace.Type, trace.SubjectId, trace.CellX, trace.CellY);

                    if (_lastShareTick.TryGetValue(key, out long lastTick))
                    {
                        if ((tick.Index - lastTick) < cooldownTicks)
                            break; // non ripetiamo
                    }

                    // NOTA IMPORTANTE (Opzione 2):
                    // - Passiamo tick.Index alla rule, così può costruire un envelope con TickIndex corretto.
                    if (rule.TryCreateToken(world, tick.Index, speakerId, listenerId, trace, out var env))
                    {
                        tokenBus.Publish(env);

                        // DEBUG: log del token emesso (non consuma il TokenBus)
                        Debug.Log($"[TokenEmit] {env}");

                        _lastShareTick[key] = tick.Index;

                        emittedThisEncounter++;
                        emittedToday++;

                        telemetry.Counter("TokenEmissionPipeline.TokensCreated", 1);
                    }

                    break; // una trace -> max un token (per ora)
                }

                if (emittedToday >= maxPerDay)
                    break;
            }

            if (emittedThisEncounter > 0)
                _tokensEmittedToday[speakerId] = emittedToday;

            return emittedThisEncounter;
        }

        private static int Manhattan(int ax, int ay, int bx, int by)
        {
            int dx = ax - bx; if (dx < 0) dx = -dx;
            int dy = ay - by; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        /// <summary>
        /// Chiave cooldown: speaker+listener+contenuto (type/subject/cell).
        /// Implementiamo hash/equals per essere veloci e deterministici.
        /// </summary>
        private readonly struct ShareKey : IEquatable<ShareKey>
        {
            private readonly int _speaker;
            private readonly int _listener;
            private readonly MemoryType _type;
            private readonly int _subject;
            private readonly int _x;
            private readonly int _y;

            public ShareKey(int speaker, int listener, MemoryType type, int subject, int x, int y)
            {
                _speaker = speaker;
                _listener = listener;
                _type = type;
                _subject = subject;
                _x = x;
                _y = y;
            }

            public bool Equals(ShareKey other)
            {
                return _speaker == other._speaker &&
                       _listener == other._listener &&
                       _type == other._type &&
                       _subject == other._subject &&
                       _x == other._x &&
                       _y == other._y;
            }

            public override bool Equals(object obj) => obj is ShareKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + _speaker;
                    h = h * 31 + _listener;
                    h = h * 31 + (int)_type;
                    h = h * 31 + _subject;
                    h = h * 31 + _x;
                    h = h * 31 + _y;
                    return h;
                }
            }
        }
    }
}