using Arcontio.Core.Diagnostics;
using System;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// ObjectPerceptionSystem:
    /// - per ogni NPC, valuta quali oggetti "interagibili" sono visibili
    /// - produce ObjectSpottedEvent per MemoryEncodingSystem
    ///
    /// Day9+:
    /// - gli occluder (muri/porte) sono oggetti nel World, MA:
    ///   - non generano ObjectSpottedEvent se IsInteractable=false
    ///   - bloccano la LOS tramite World.OcclusionMap
    /// </summary>
    public sealed class ObjectPerceptionSystem : ISystem
    {
        public int Period => 1;

        private readonly List<int> _npcIds = new(2048);
        private readonly List<int> _objIds = new(2048);

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (world.Objects.Count == 0 || world.NpcCore.Count == 0)
                return;

            int visionRange = world.Global.NpcVisionRangeCells;
            if (visionRange <= 0) visionRange = 6;

            bool useCone = world.Global.NpcVisionUseCone;
            float coneSlope = world.Global.NpcVisionConeSlope;

            // Back-compat: se stai ancora usando "NpcVisionConeHalfWidthPerStep"
            // e NpcVisionConeSlope non è impostato, copia.
            if (coneSlope <= 0f && world.Global.NpcVisionConeHalfWidthPerStep > 0f)
                coneSlope = world.Global.NpcVisionConeHalfWidthPerStep;

            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            _objIds.Clear();
            _objIds.AddRange(world.Objects.Keys);

            int spotted = 0;

            for (int n = 0; n < _npcIds.Count; n++)
            {
                int npcId = _npcIds[n];
                if (!world.GridPos.TryGetValue(npcId, out var np))
                    continue;

                if (!world.NpcFacing.TryGetValue(npcId, out var facing))
                    facing = CardinalDirection.North;

                for (int o = 0; o < _objIds.Count; o++)
                {
                    int objId = _objIds[o];
                    if (!world.Objects.TryGetValue(objId, out var obj) || obj == null)
                        continue;

                    // filtro: solo oggetti definiti e interagibili
                    if (!world.TryGetObjectDef(obj.DefId, out var def) || def == null)
                        continue;

                    if (!def.IsInteractable)
                        continue;

                    int dist = Manhattan(np.X, np.Y, obj.CellX, obj.CellY);
                    if (dist > visionRange)
                        continue;

                    // Cone check (opzionale)
                    if (useCone)
                    {
                        if (!IsInCone(np.X, np.Y, facing, obj.CellX, obj.CellY, coneSlope))
                            continue;
                    }
                    else
                    {
                        // modalità legacy: "davanti" (linea frontale)
                        if (!IsInFront(np.X, np.Y, facing, obj.CellX, obj.CellY))
                            continue;
                    }

                    // LOS check (questo è il pezzo che ti mancava nel T9)
                    if (!world.HasLineOfSight(np.X, np.Y, obj.CellX, obj.CellY))
                        continue;

                    float q = 1f - (dist / (float)visionRange);
                    if (q < 0.05f) q = 0.05f;

                    bus.Publish(new ObjectSpottedEvent(
                        observerNpcId: npcId,
                        objectId: objId,
                        defId: obj.DefId,
                        cellX: obj.CellX,
                        cellY: obj.CellY,
                        witnessQuality01: q));

                    spotted++;
                }
            }

            telemetry.Counter("ObjectPerception.SpottedEvents", spotted);
        }

        private static int Manhattan(int ax, int ay, int bx, int by)
        {
            int dx = ax - bx; if (dx < 0) dx = -dx;
            int dy = ay - by; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        private static bool IsInFront(int sx, int sy, CardinalDirection facing, int tx, int ty)
        {
            int dx = tx - sx;
            int dy = ty - sy;

            return facing switch
            {
                CardinalDirection.North => dy > 0 && dx == 0,
                CardinalDirection.South => dy < 0 && dx == 0,
                CardinalDirection.East => dx > 0 && dy == 0,
                CardinalDirection.West => dx < 0 && dy == 0,
                _ => false
            };
        }

        /// <summary>
        /// Cono su griglia:
        /// - forward deve essere > 0
        /// - |side| <= floor(forward * slope)
        /// slope:
        /// - 0.0 => linea
        /// - 0.5 => cono stretto
        /// - 1.0 => cono ampio (circa 45° su Manhattan grid)
        /// </summary>
        private static bool IsInCone(int sx, int sy, CardinalDirection facing, int tx, int ty, float slope)
        {
            int dx = tx - sx;
            int dy = ty - sy;

            int forward, side;

            switch (facing)
            {
                case CardinalDirection.North:
                    forward = dy; side = dx; break;
                case CardinalDirection.South:
                    forward = -dy; side = -dx; break;
                case CardinalDirection.East:
                    forward = dx; side = -dy; break;
                case CardinalDirection.West:
                    forward = -dx; side = dy; break;
                default:
                    return false;
            }

            if (forward <= 0)
                return false;

            int absSide = side < 0 ? -side : side;

            // floor(forward * slope)
            int maxSide = (int)Math.Floor((forward * slope) + 0.0001f);
            return absSide <= maxSide;
        }
    }
}
