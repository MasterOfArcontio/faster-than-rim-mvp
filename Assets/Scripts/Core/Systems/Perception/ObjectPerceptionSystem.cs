using System;
using System.Collections.Generic;

namespace Arcontio.Core
{
    /// <summary>
    /// ObjectPerceptionSystem (Giorno 8 - “vista -> ricordo”):
    ///
    /// - Scorre NPC e oggetti
    /// - Se un oggetto è nel range visivo e nel campo davanti (orientamento),
    ///   emette ObjectSpottedEvent nel MessageBus.
    ///
    /// V0 (semplice, deterministica):
    /// - VisionRange globale: world.Global.NpcVisionRangeCells
    /// - FOV: A CONO (grid cone) davanti all'NPC.
    ///   In pratica: forwardDist > 0 e sideDist <= forwardDist.
    ///
    /// Nota:
    /// - Non scrive memoria direttamente: produce eventi (verità del mondo).
    /// - MemoryEncodingSystem farà il resto.
    /// PATCH: ora il FOV è "a cono" davanti all’NPC (non solo una linea).
    /// - Forward/Side in base a facing (N/S/E/W)
    /// - Condizione: forward > 0 AND abs(side) <= forward * coneHalfWidthPerStep
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

            int vision = world.Global.NpcVisionRangeCells;
            if (vision <= 0) vision = 6;

            // NEW: ampiezza cono (0 = linea, 1 = cono ampio)
            float coneHalfWidthPerStep = world.Global.NpcVisionConeHalfWidthPerStep;
            if (coneHalfWidthPerStep < 0f) coneHalfWidthPerStep = 0f;

            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            _objIds.Clear();
            _objIds.AddRange(world.Objects.Keys);

            int spotted = 0;

            for (int n = 0; n < _npcIds.Count; n++)
            {
                int npcId = _npcIds[n];
                if (!world.GridPos.TryGetValue(npcId, out var np)) continue;

                if (!world.NpcFacing.TryGetValue(npcId, out var facing))
                    facing = CardinalDirection.North;

                for (int o = 0; o < _objIds.Count; o++)
                {
                    int objId = _objIds[o];
                    if (!world.Objects.TryGetValue(objId, out var obj) || obj == null) continue;

                    int dist = Manhattan(np.X, np.Y, obj.CellX, obj.CellY);
                    if (dist > vision) continue;

                    // NEW: FOV a cono (prima era solo "davanti in linea")
                    if (!IsInCone(np.X, np.Y, facing, obj.CellX, obj.CellY, coneHalfWidthPerStep))
                        continue;

                    float q = 1f - (dist / (float)vision);
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

        /// <summary>
        /// IsInCone:
        /// Cono in griglia deterministico, basato su:
        /// - forward: quanto è davanti (deve essere > 0)
        /// - side: quanto è laterale (|side| <= forward * coneHalfWidthPerStep)
        ///
        /// coneHalfWidthPerStep:
        /// - 0.0  => solo linea frontale
        /// - 0.5  => cono stretto
        /// - 1.0  => cono più ampio (45° approx su griglia)
        /// </summary>
        private static bool IsInCone(int sx, int sy, CardinalDirection facing, int tx, int ty, float coneHalfWidthPerStep)
        {
            int dx = tx - sx;
            int dy = ty - sy;

            int forward, side;

            switch (facing)
            {
                case CardinalDirection.North:
                    forward = dy;
                    side = dx;
                    break;

                case CardinalDirection.South:
                    forward = -dy;
                    side = -dx;
                    break;

                case CardinalDirection.East:
                    forward = dx;
                    side = -dy;
                    break;

                case CardinalDirection.West:
                    forward = -dx;
                    side = dy;
                    break;

                default:
                    return false;
            }

            if (forward <= 0)
                return false;

            // conoHalfWidthPerStep=0 -> richiede side==0 (linea)
            int absSide = side < 0 ? -side : side;

            // confronto evitando float “pesanti”: absSide <= forward * coneHalfWidthPerStep
            // qui usiamo float perché coneHalfWidthPerStep è configurabile.
            return absSide <= (int)Math.Floor(forward * coneHalfWidthPerStep + 0.0001f);
        }
    }
}

