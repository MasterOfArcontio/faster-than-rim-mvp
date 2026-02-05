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

        // Component store minimalista (tipo ECS)
        public readonly Dictionary<int, NpcCore> NpcCore = new();
        public readonly Dictionary<int, Needs> Needs = new();
        public readonly Dictionary<int, Social> Social = new();

        public readonly Dictionary<int, GridPosition> GridPos = new();

        public readonly Dictionary<int, MemoryStore> Memory = new();

        public readonly Dictionary<int, PersonalityMemoryParams> MemoryParams = new();
        
        // Store sparse: solo celle che hanno un occluder vengono salvate.
        public readonly Dictionary<long, Occluder> Occluders = new();

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

            return id;
        }

        public bool ExistsNpc(int id) => NpcCore.ContainsKey(id);
        public bool AreBonded(int aNpcId, int bNpcId)
        {
            // STUB (roadmap): quando introdurrai bond graph,
            // questa funzione consulterà quel grafo.
            return false;
        }
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
