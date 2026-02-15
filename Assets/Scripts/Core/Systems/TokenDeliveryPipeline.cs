using Arcontio.Core.Diagnostics;
using Arcontio.Core.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Core
{
    public sealed class TokenDeliveryPipeline
    {
        private readonly List<TokenEnvelope> _outBuffer = new(256);

        // BFS buffers (riusabili)
        private readonly Queue<(int x, int y)> _q = new();
        private readonly Dictionary<long, int> _dist = new(); // cellKey -> distance

        public void Deliver(World world, Tick tick, TokenBus tokenBusOut, TokenBus tokenBusIn, Telemetry telemetry)
        {
            tokenBusOut.DrainTo(_outBuffer);
            if (_outBuffer.Count == 0) return;

            int maxRange = world.Global.TokenDeliveryMaxRangeCells;
            if (maxRange <= 0) maxRange = 1;

            bool enableLos = world.Global.EnableTokenLOS;

            float relFalloff = world.Global.TokenReliabilityFalloffPerCell;
            if (relFalloff < 0f) relFalloff = 0f;

            float intFalloff = world.Global.TokenIntensityFalloffPerCell;
            if (intFalloff < 0f) intFalloff = 0f;

            int delivered = 0, droppedRange = 0, droppedLos = 0, droppedTooWeak = 0;

            for (int i = 0; i < _outBuffer.Count; i++)
            {
                var env = _outBuffer[i];

                if (!world.GridPos.TryGetValue(env.SpeakerId, out var sp)) continue;
                if (!world.GridPos.TryGetValue(env.ListenerId, out var li)) continue;

                // Distanza “effettiva” usata per falloff:
                // - ProximityTalk/TargetedVisit: Manhattan (e LOS se attivo)
                // - AlarmShout: BFS detour distance (aggira muri)
                int effectiveDist;

                if (env.Channel == TokenChannel.AlarmShout)
                {
                    // BFS “acustico”: distanza del percorso più corto aggirando muri
                    if (!TryGetBfsDistance(world, sp.X, sp.Y, li.X, li.Y, maxRange, out effectiveDist))
                    {
                        droppedRange++;
                        continue;
                    }
                }
                else
                {
                    // Voce “normale”: distanza Manhattan
                    effectiveDist = Manhattan(sp.X, sp.Y, li.X, li.Y);
                    if (effectiveDist > maxRange)
                    {
                        droppedRange++;
                        continue;
                    }

                    // LOS solo su canali “ottici”
                    if (enableLos)
                    {
                        if (HasBlockingLOS(world, sp.X, sp.Y, li.X, li.Y))
                        {
                            droppedLos++;
                            continue;
                        }
                    }
                }

                // Falloff base con distanza effettiva
                float newRel = env.Token.Reliability01 - (relFalloff * effectiveDist);
                float newInt = env.Token.Intensity01 - (intFalloff * effectiveDist);

                // Clamp + drop se troppo debole
                if (newRel < 0f) newRel = 0f;
                if (newInt < 0f) newInt = 0f;

                if (newRel < 0.01f || newInt < 0.01f)
                {
                    droppedTooWeak++;
                    continue;
                }

                // Ricrea token degradato
                var old = env.Token;
                var degraded = new SymbolicToken(
                    type: old.Type,
                    subjectId: old.SubjectId,
                    intensity01: newInt,
                    reliability01: newRel,
                    chainDepth: old.ChainDepth,
                    hasCell: old.HasCell,
                    cellX: old.CellX,
                    cellY: old.CellY
                );

                var arrived = new TokenEnvelope(
                    speakerId: env.SpeakerId,
                    listenerId: env.ListenerId,
                    channel: env.Channel,
                    tickIndex: env.TickIndex,
                    token: degraded
                );

                tokenBusIn.Publish(arrived);
                delivered++;

                // DEBUG throttled: così vedi “muro corto vs muro lungo”
                if ((tick.Index % 50) == 0)
                {
                    ArcontioLogger.Debug(
                        new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "TokenDelivery"),
                        new LogBlock(LogLevel.Debug, "log.tokendelivery.updated")
                            .AddField("channel", env.Channel)
                            .AddField("route", $"{env.SpeakerId}->{env.ListenerId}")
                            .AddField("dist", effectiveDist)
                            .AddField("rel", $"{env.Token.Reliability01:0.00}->{newRel:0.00}")
                            .AddField("int", $"{env.Token.Intensity01:0.00}->{newInt:0.00}")
                    );
                }
            }

            telemetry.Counter("TokenDelivery.Delivered", delivered);
            telemetry.Counter("TokenDelivery.DroppedRange", droppedRange);
            telemetry.Counter("TokenDelivery.DroppedLOS", droppedLos);
            telemetry.Counter("TokenDelivery.DroppedTooWeak", droppedTooWeak);
        }

        private static int Manhattan(int ax, int ay, int bx, int by)
        {
            int dx = ax - bx; if (dx < 0) dx = -dx;
            int dy = ay - by; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        // =========================
        // LOS “ottico” (blocca)
        // =========================
        private static bool HasBlockingLOS(World world, int x0, int y0, int x1, int y1)
        {
            foreach (var cell in BresenhamCellsBetween(x0, y0, x1, y1))
            {
                if (!world.TryGetOccluder(cell.x, cell.y, out var occ))
                    continue;

                if (!occ.BlocksVision)
                    continue;

                // v0: se è un muro pieno, blocchiamo del tutto
                float cost = occ.VisionCost;
                if (cost >= 1f)
                    return true;
            }
            return false;
        }

        private static IEnumerable<(int x, int y)> BresenhamCellsBetween(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);

            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;

            int err = dx - dy;
            int x = x0;
            int y = y0;

            while (!(x == x1 && y == y1))
            {
                int e2 = 2 * err;

                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }

                if (x == x1 && y == y1) yield break;
                if (x == x0 && y == y0) continue;

                yield return (x, y);
            }
        }

        // =========================
        // BFS “acustico” (detour)
        // =========================
        private bool TryGetBfsDistance(World world, int sx, int sy, int tx, int ty, int maxRange, out int outDist)
        {
            _q.Clear();
            _dist.Clear();

            long startKey = CellKey(sx, sy);
            _dist[startKey] = 0;
            _q.Enqueue((sx, sy));

            while (_q.Count > 0)
            {
                var (x, y) = _q.Dequeue();
                int d = _dist[CellKey(x, y)];

                if (d > maxRange)
                    continue;

                if (x == tx && y == ty)
                {
                    outDist = d;
                    return true;
                }

                // 4-neighborhood
                TryEnqueue(world, x + 1, y, d + 1);
                TryEnqueue(world, x - 1, y, d + 1);
                TryEnqueue(world, x, y + 1, d + 1);
                TryEnqueue(world, x, y - 1, d + 1);
            }

            outDist = 0;
            return false;
        }

        private void TryEnqueue(World world, int x, int y, int newDist)
        {
            // v0: il suono non attraversa muri “fisici”
            // (se vuoi usare BlocksVision invece, cambia qui)
            if (world.TryGetOccluder(x, y, out var occ))
            {
                if (occ.BlocksMovement && occ.VisionCost >= 1f)
                    return;
            }

            long key = CellKey(x, y);
            if (_dist.ContainsKey(key))
                return;

            _dist[key] = newDist;
            _q.Enqueue((x, y));
        }

        private static long CellKey(int x, int y) => ((long)x << 32) ^ (uint)y;
    }
}
