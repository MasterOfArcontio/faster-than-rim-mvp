using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// World: contiene TUTTO lo stato simulato (NPC, risorse, gruppi, leggi, ecc.).
    /// Nessuna logica qui: solo dati e query semplici.
    /// </summary>
    public sealed class World
    {
        // Esempio: entità NPC identificate da int
        private int _nextNpcId = 1;

        // NEW (Giorno 8): id incrementale per oggetti nel mondo (istanze)
        private int _nextObjectId = 1;

        // Component store minimalista (tipo ECS)
        public readonly Dictionary<int, NpcCore> NpcCore = new();
        public readonly Dictionary<int, Needs> Needs = new();
        public readonly Dictionary<int, Social> Social = new();

        public readonly Dictionary<int, GridPosition> GridPos = new();

        // NEW (Giorno 8): orientamento NPC (N/S/E/W).
        // Serve per:
        // - Perception: cosa vede un NPC “davanti a sé”
        // - Comunicazione: chi parla con chi in modo credibile (no telepatia)
        public readonly Dictionary<int, CardinalDirection> NpcFacing = new();

        public readonly Dictionary<int, MemoryStore> Memory = new();

        public readonly Dictionary<int, PersonalityMemoryParams> MemoryParams = new();

        // Store sparse: solo celle che hanno un occluder vengono salvate.
        public readonly Dictionary<long, Occluder> Occluders = new();

        // NEW (Giorno 8): definizioni oggetti caricate da JSON (template)
        // key = defId (es. "bed_wood_poor")
        public readonly Dictionary<string, ObjectDef> ObjectDefs = new();

        // NEW (Giorno 8): istanze nel mondo (objId -> instance)
        public readonly Dictionary<int, WorldObjectInstance> Objects = new();

        // Stato "macro" (risorse comuni, leggi, ecc.) - placeholder
        public GlobalState Global = new();

        public int CreateNpc(NpcCore core, Needs needs, Social social, int x, int y)
        {
            int id = _nextNpcId++;

            NpcCore.Add(id, core);
            Needs.Add(id, needs);
            Social.Add(id, social);
            GridPos.Add(id, new GridPosition(x, y));

            // Ogni NPC ha un proprio store di memoria (inizialmente vuoto).
            Memory.Add(id, new MemoryStore());

            // NEW: parametri intrinseci di memoria (tratti stabili)
            MemoryParams.Add(id, PersonalityMemoryParams.DefaultNpc());

            // NEW (Giorno 8): orientamento default
            NpcFacing.Add(id, CardinalDirection.North);

            return id;
        }

        public bool ExistsNpc(int id) => NpcCore.ContainsKey(id);

        public bool AreBonded(int aNpcId, int bNpcId)
        {
            // STUB (roadmap): quando introdurrai bond graph,
            // questa funzione consulterà quel grafo.
            return false;
        }

        // ----------------------------
        // Occluders (già presenti)
        // ----------------------------
        public bool TryGetOccluder(int x, int y, out Occluder occ)
        {
            long key = CellKey(x, y);
            return Occluders.TryGetValue(key, out occ);
        }

        public void SetOccluder(int x, int y, Occluder occ)
        {
            long key = CellKey(x, y);
            Occluders[key] = occ;
        }

        /// <summary>
        /// Converte (x,y) in una chiave long deterministica.
        /// Importante: evita allocazioni e collisioni pratiche.
        /// </summary>
        private static long CellKey(int x, int y)
        {
            // packing int32 + int32 in int64
            return ((long)x << 32) ^ (uint)y;
        }

        // ----------------------------
        // NEW (Giorno 8): Objects API
        // ----------------------------

        /// <summary>
        /// Crea un oggetto nel mondo in una cella.
        /// - defId deve esistere in ObjectDefs (caricato da JSON)
        /// - owner opzionale
        /// </summary>
        public int CreateObject(string defId, int x, int y, OwnerKind ownerKind = OwnerKind.None, int ownerId = -1)
        {
            if (string.IsNullOrWhiteSpace(defId))
                return -1;

            if (!ObjectDefs.ContainsKey(defId))
            {
                Debug.LogWarning($"[World] CreateObject failed: unknown defId='{defId}'");
                return -1;
            }

            int id = _nextObjectId++;

            var inst = new WorldObjectInstance
            {
                ObjectId = id,
                DefId = defId,
                CellX = x,
                CellY = y,
                OwnerKind = ownerKind,
                OwnerId = ownerId,
                OccupantNpcId = -1
            };

            Objects[id] = inst;
            return id;
        }

        public bool TryGetObject(int objectId, out WorldObjectInstance inst)
            => Objects.TryGetValue(objectId, out inst);

        public bool TryGetObjectDef(string defId, out ObjectDef def)
            => ObjectDefs.TryGetValue(defId, out def);

        public void SetFacing(int npcId, CardinalDirection dir)
        {
            if (!NpcFacing.ContainsKey(npcId)) return;
            NpcFacing[npcId] = dir;
        }
    }

    /// <summary>
    /// Stato globale del mondo (placeholder): risorse comuni, meteo/eventi, leggi, ecc.
    /// </summary>
    public struct GlobalState
    {
        public int FoodStock;
        public int MaterialsStock;

        // Leadership ufficiale/accettata (se la userai nel simulatore)
        public int AcceptedLeaderId;

        // --- Memory Spatial Fusion (feature flag + params) ---
        public bool EnableMemorySpatialFusion;   // default false
        public int MemoryRegionSizeCells;        // es. 4

        public int MaxTokensPerEncounter;
        public int MaxTokensPerNpcPerDay;
        public int RepeatShareCooldownTicks;

        // Giorno 7: delivery params
        public int TokenDeliveryMaxRangeCells;
        public bool EnableTokenLOS;
        public float TokenReliabilityFalloffPerCell;
        public float TokenIntensityFalloffPerCell;

        // NEW (Giorno 8): Perception base (temporaneo)
        // Range visivo usato per:
        // - ObjectPerceptionSystem (vede letti/workbench ecc.)
        public int NpcVisionRangeCells;
        public float NpcVisionConeHalfWidthPerStep; // NEW (Day8): 0=linea, 1=cono ampio

    }

    // Determina una posizione spaziale nella griglia del simulatore
    public struct GridPosition
    {
        public int X;
        public int Y;

        public GridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
