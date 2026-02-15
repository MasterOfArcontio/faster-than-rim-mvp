using Arcontio.Core.Diagnostics;
using Arcontio.Core.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// NeedsDecisionRule (Day9):
    /// Rule alto livello, coerente col tuo stile:
    /// - Reagisce a eventi (TickPulseEvent) e produce Commands.
    ///
    /// Decisione v0:
    /// - se hungry: privato -> stock community (VISIBILE) -> furto (se moralità/emergenza)
    /// - se tired: letto community libero (VISIBILE) -> letto altrui (moralità/emergenza)
    ///
    /// Nota Day9:
    /// - “VISIBILE” qui lo modelliamo con memoria/conoscenza.
    ///   V0 semplice: se l’oggetto è stato “spotted” (Day8) allora è noto.
    ///   Se non hai ancora una MemoryType dedicata per “ObjectKnown”, puoi usare:
    ///   - MemoryStore.ContainsObject(defId/objId) (se l’hai)
    ///   - oppure fallback: range/LOS diretto (per il test).
    /// </summary>
    public sealed class NeedsDecisionRule : IRule
    {
        // Throttle: decidiamo ogni N tick-pulse (per log leggibile)
        private readonly int _decisionEveryTicks;

        public NeedsDecisionRule(int decisionEveryTicks = 25)
        {
            _decisionEveryTicks = decisionEveryTicks < 1 ? 1 : decisionEveryTicks;
        }

        public void Handle(World world, ISimEvent e, List<ICommand> outCommands, Telemetry telemetry)
        {
            // Usiamo TickPulseEvent come “clock” decisionale.
            if (e is not TickPulseEvent pulse)
                return;

            if ((pulse.TickIndex % _decisionEveryTicks) != 0)
                return;

            var cfg = world.Global.Needs;

            int ate = 0, slept = 0, stole = 0;

            foreach (var npcId in world.NpcCore.Keys)
            {
                if (!world.Needs.TryGetValue(npcId, out var needs)) continue;

                // --- MANGIA ---
                if (needs.Hunger01 >= cfg.hungryThreshold)
                {
                    if (TryPlanEat(world, npcId, needs, out var cmd, out bool didSteal))
                    {
                        outCommands.Add(cmd);
                        ate++;
                        if (didSteal) stole++;
                    }
                }

                // --- DORMI ---
                if (needs.Fatigue01 >= cfg.tiredThreshold)
                {
                    if (TryPlanSleep(world, npcId, needs, out var sleepCmd))
                    {
                        outCommands.Add(sleepCmd);
                        slept++;
                    }
                }
            }

            telemetry.Counter("Day9.PlanEat", ate);
            telemetry.Counter("Day9.PlanSleep", slept);
            telemetry.Counter("Day9.PlanSteal", stole);

            ArcontioLogger.Debug(
                new LogContext(tick: pulse.TickIndex, channel: "T9"),
                new LogBlock(LogLevel.Debug, "log.t9.plan.summary")
                    .AddField("eatCmds", ate)
                    .AddField("sleepCmds", slept)
                    .AddField("stealCmds", stole)
            );
        }

        private static bool TryPlanEat(World world, int npcId, in Needs needs, out ICommand cmd, out bool didSteal)
        {
            cmd = null;
            didSteal = false;

            // 1) privato
            if (world.NpcPrivateFood.TryGetValue(npcId, out int priv) && priv > 0)
            {
                cmd = new EatPrivateFoodCommand(npcId);
                return true;
            }

            // 2) stock community “conosciuto”
            // v0: prendiamo il primo stock community con Units>0.
            // (Se vuoi legarlo alla conoscenza: filtra solo stock “visto” dall’NPC via memorie Day8.)
            int foodObj = FindCommunityFoodStock(world);
            if (foodObj != 0)
            {
                cmd = new EatFromStockCommand(npcId, foodObj);
                return true;
            }

            // 3) furto se moralità/emergenza
            float law = world.Social.TryGetValue(npcId, out var soc) ? soc.JusticePerception01 : 0.5f;
            bool emergency = needs.Hunger01 >= 0.95f;
            bool okToSteal = emergency || law < 0.45f;

            if (!okToSteal)
                return false;

            int victim = FindNpcWithPrivateFood(world, npcId);
            if (victim != 0)
            {
                didSteal = true;
                cmd = new StealPrivateFoodCommand(npcId, victim);
                return true;
            }

            return false;
        }

        private static bool TryPlanSleep(World world, int npcId, in Needs needs, out ICommand cmd)
        {
            cmd = null;

            // 1) letto community libero
            int bedCommunity = FindBed(world, OwnerKind.Community, 0);
            if (bedCommunity != 0 && !world.GetUseStateOrDefault(bedCommunity).IsInUse)
            {
                cmd = new SleepInBedCommand(npcId, bedCommunity, "Community");
                return true;
            }

            // 2) letto altrui se moralità/emergenza
            float law = world.Social.TryGetValue(npcId, out var soc) ? soc.JusticePerception01 : 0.5f;
            bool emergency = needs.Fatigue01 >= 0.95f;
            bool okToTrespass = emergency || law < 0.45f;

            if (!okToTrespass)
                return false;

            int bedOther = FindAnyOwnedBedNotNpc(world, npcId);
            if (bedOther != 0 && !world.GetUseStateOrDefault(bedOther).IsInUse)
            {
                cmd = new SleepInBedCommand(npcId, bedOther, "Trespass");
                return true;
            }

            return false;
        }

        // ------- helpers v0 (semplici) -------

        private static int FindCommunityFoodStock(World world)
        {
            foreach (var kv in world.FoodStocks)
            {
                int objId = kv.Key;
                var st = kv.Value;
                if (st.Units <= 0) continue;
                if (st.OwnerKind == OwnerKind.Community && st.OwnerId == 0)
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

                // v0: “è un letto” se defId contiene "bed"
                if (string.IsNullOrWhiteSpace(obj.DefId)) continue;
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
                if (string.IsNullOrWhiteSpace(obj.DefId)) continue;
                if (!obj.DefId.Contains("bed")) continue;

                if (obj.OwnerKind == OwnerKind.Npc && obj.OwnerId != npcId)
                    return objId;
            }
            return 0;
        }
    }
}
