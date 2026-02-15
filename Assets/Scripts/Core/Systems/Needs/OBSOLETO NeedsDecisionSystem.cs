using System.Collections.Generic;
using UnityEngine;
using Arcontio.Core.Diagnostics;

namespace Arcontio.Core
{
    /// <summary>
    /// NeedsDecisionSystem (Day9):
    /// Decisione deterministica v0 per:
    /// - mangiare (privato -> libero -> furto)
    /// - dormire (letto libero -> altrui se "moralità" lo consente)
    ///
    /// Nota architetturale:
    /// - Questo è un System "di gameplay deterministico".
    /// - Pubblica eventi (FoodStolenEvent) per memoria/logica downstream.
    /// - Non usa Commands/Rules in questa v0: mantiene il test semplice.
    /// </summary>
    public sealed class OBSOLETO_NeedsDecisionSystem : ISystem
    {
        public int Period => 1;

        private readonly List<int> _npcIds = new(2048);

        public void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry)
        {
            if (world.NpcCore.Count == 0) return;

            var cfg = world.Global.Needs;

            _npcIds.Clear();
            _npcIds.AddRange(world.NpcCore.Keys);

            int ate = 0, slept = 0, stole = 0;

            for (int i = 0; i < _npcIds.Count; i++)
            {
                int npcId = _npcIds[i];

                if (!world.Needs.TryGetValue(npcId, out var needs))
                    continue;

                // Social è un tuo component store (World.Social).
                // Se non esiste, usiamo default (valori 0).
                if (!world.Social.TryGetValue(npcId, out var social))
                    social = default;

                // Hunger decision
                if (needs.Hunger01 >= cfg.hungryThreshold)
                {
                    if (TryEat(world, npcId, social, ref needs, cfg, bus, telemetry, out bool didSteal))
                    {
                        ate++;
                        if (didSteal) stole++;
                    }
                }

                // Fatigue decision
                if (needs.Fatigue01 >= cfg.tiredThreshold)
                {
                    if (TrySleep(world, npcId, social, ref needs, cfg, telemetry))
                        slept++;
                }

                // Persistiamo le modifiche al component store
                world.Needs[npcId] = needs;
            }

            telemetry.Counter("Day9.EatActions", ate);
            telemetry.Counter("Day9.SleepActions", slept);
            telemetry.Counter("Day9.TheftActions", stole);

            // Log sintetico throttled
            if ((tick.Index % 25) == 0)
                Debug.Log($"[T9] tick={tick.Index} ate={ate} slept={slept} stole={stole}");
        }

        private static bool TryEat(
            World world,
            int npcId,
            Social social,               // <-- FIX: ora social è disponibile anche dentro TryEat
            ref Needs needs,
            NeedsConfig cfg,
            MessageBus bus,
            Telemetry telemetry,
            out bool didSteal)
        {
            didSteal = false;

            // 1) Private food first
            if (world.NpcPrivateFood.TryGetValue(npcId, out int priv) && priv > 0)
            {
                world.NpcPrivateFood[npcId] = priv - 1;

                needs.Hunger01 -= cfg.eatSatietyGain;
                if (needs.Hunger01 < 0f) needs.Hunger01 = 0f;

                Debug.Log($"[T9][Eat] npc={npcId} ate PRIVATE. privLeft={world.NpcPrivateFood[npcId]} hungerNow={needs.Hunger01:0.00}");
                return true;
            }

            // 2) Community stock
            int communityFoodObj = FindFoodObject(world, ownerKind: OwnerKind.Community, ownerId: 0);
            if (communityFoodObj != 0 && world.FoodStocks.TryGetValue(communityFoodObj, out var stock) && stock.Units > 0)
            {
                stock.Units -= 1;
                world.FoodStocks[communityFoodObj] = stock;

                needs.Hunger01 -= cfg.eatSatietyGain;
                if (needs.Hunger01 < 0f) needs.Hunger01 = 0f;

                Debug.Log($"[T9][Eat] npc={npcId} ate COMMUNITY stock. stockLeft={stock.Units} hungerNow={needs.Hunger01:0.00}");
                return true;
            }

            // 3) Steal if allowed by "legalità"
            // JusticePerception01 alto => non ruba (se non in emergenza)
            bool emergency = needs.Hunger01 >= 0.95f;
            float law = social.JusticePerception01;
            bool okToSteal = emergency || law < 0.45f;

            if (!okToSteal)
            {
                Debug.Log($"[T9][Eat] npc={npcId} wanted food but REFUSED to steal (law={law:0.00}, hunger={needs.Hunger01:0.00}).");
                return false;
            }

            // Cerca vittima con food privato
            int victimNpc = FindNpcWithPrivateFood(world, exceptNpcId: npcId);
            if (victimNpc != 0)
            {
                int victimFood = world.NpcPrivateFood[victimNpc];
                if (victimFood > 0)
                {
                    world.NpcPrivateFood[victimNpc] = victimFood - 1;

                    needs.Hunger01 -= cfg.eatSatietyGain;
                    if (needs.Hunger01 < 0f) needs.Hunger01 = 0f;

                    didSteal = true;

                    // Evento -> MemoryEncodingSystem -> FoodStolenMemoryRule (vittima)
                    //bus.Publish(new FoodStolenEvent(victimNpcId: victimNpc, thiefNpcId: npcId, units: 1));

                    Debug.Log($"[T9][STEAL] thief={npcId} stole 1 food from victim={victimNpc}. victimLeft={world.NpcPrivateFood[victimNpc]} hungerNow={needs.Hunger01:0.00}");
                    telemetry.Counter("Day9.FoodStolenEvents", 1);
                    return true;
                }
            }

            Debug.Log($"[T9][Eat] npc={npcId} could not find food (even to steal).");
            return false;
        }

        private static bool TrySleep(World world, int npcId, Social social, ref Needs needs, NeedsConfig cfg, Telemetry telemetry)
        {
            // 1) Letto community libero
            int bedCommunity = FindBed(world, ownerKind: OwnerKind.Community, ownerId: 0);
            if (bedCommunity != 0 && !world.GetUseStateOrDefault(bedCommunity).IsInUse)
            {
                var s = world.GetUseStateOrDefault(bedCommunity);
                s.IsInUse = true;
                s.UsingNpcId = npcId;
                world.SetUseState(bedCommunity, s);

                needs.Fatigue01 -= cfg.sleepRestGainPerTick;
                if (needs.Fatigue01 < 0f) needs.Fatigue01 = 0f;

                Debug.Log($"[T9][Sleep] npc={npcId} slept in COMMUNITY bed obj={bedCommunity}. fatigueNow={needs.Fatigue01:0.00}");
                telemetry.Counter("Day9.BedUseCommunity", 1);
                return true;
            }

            // 2) Trespass letto altrui solo in emergenza o se “poca legalità”
            bool emergency = needs.Fatigue01 >= 0.95f;
            float law = social.JusticePerception01;
            bool okToTrespass = emergency || law < 0.45f;

            if (!okToTrespass)
                return false;

            int bedOther = FindAnyOwnedBedNotNpc(world, npcId);
            if (bedOther != 0 && !world.GetUseStateOrDefault(bedOther).IsInUse)
            {
                var s2 = world.GetUseStateOrDefault(bedOther);
                s2.IsInUse = true;
                s2.UsingNpcId = npcId;
                world.SetUseState(bedOther, s2);

                needs.Fatigue01 -= cfg.sleepRestGainPerTick;
                if (needs.Fatigue01 < 0f) needs.Fatigue01 = 0f;

                Debug.Log($"[T9][SleepTrespass] npc={npcId} used OTHER bed obj={bedOther}. fatigueNow={needs.Fatigue01:0.00}");
                telemetry.Counter("Day9.BedUseTrespass", 1);
                return true;
            }

            return false;
        }

        // -------- find helpers (v0) --------

        private static int FindFoodObject(World world, OwnerKind ownerKind, int ownerId)
        {
            foreach (var kv in world.FoodStocks)
            {
                int objId = kv.Key;
                var st = kv.Value;
                if (st.Units <= 0) continue;
                if (st.OwnerKind == ownerKind && st.OwnerId == ownerId)
                    return objId;
            }
            return 0;
        }

        private static int FindNpcWithPrivateFood(World world, int exceptNpcId)
        {
            foreach (var kv in world.NpcPrivateFood)
            {
                if (kv.Key == exceptNpcId) continue;
                if (kv.Value > 0) return kv.Key;
            }
            return 0;
        }

        private static int FindBed(World world, OwnerKind ownerKind, int ownerId)
        {
            foreach (var kv in world.Objects)
            {
                int objId = kv.Key;
                var obj = kv.Value;
                if (obj == null) continue;
                if (string.IsNullOrEmpty(obj.DefId)) continue;

                // v0: letto = defId contiene "bed"
                if (!obj.DefId.Contains("bed")) continue;

                if (obj.OwnerKind == ownerKind && obj.OwnerId == ownerId)
                    return objId;
            }
            return 0;
        }

        private static int FindAnyOwnedBedNotNpc(World world, int npcId)
        {
            foreach (var kv in world.Objects)
            {
                int objId = kv.Key;
                var obj = kv.Value;
                if (obj == null) continue;
                if (string.IsNullOrEmpty(obj.DefId)) continue;
                if (!obj.DefId.Contains("bed")) continue;

                if (obj.OwnerKind == OwnerKind.Npc && obj.OwnerId != npcId)
                    return objId;
            }
            return 0;
        }
    }
}
