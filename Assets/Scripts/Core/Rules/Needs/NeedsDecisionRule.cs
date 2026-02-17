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
    /// - se tired: letto community libero (VISIBILE) -> letto altrui (se moralità/emergenza)
    ///
    /// IMPORTANTISSIMO (patch):
    /// In ARCONTIO la visibilità non è "telepatia".
    /// Se un oggetto è dietro un muro, la decisione *non deve* poterlo usare come se fosse noto.
    ///
    /// Per questo motivo qui applichiamo un filtro "Visible" minimale:
    /// - posizione NPC: world.GridPos[npcId]
    /// - posizione oggetto: world.Objects[objId].CellX/CellY
    /// - visibilità: world.HasLineOfSight(nx,ny,ox,oy) + range discreto
    ///
    /// Nota pragmatica:
    /// - NON applichiamo il cono FOV (orientamento) in questa rule,
    ///   perché per una decisione "mangio" spesso ti interessa la *conoscenza pratica* dell'oggetto
    ///   (es. lo hai visto un secondo fa, ti giri, ecc.).
    /// - Se in futuro vuoi coerenza totale col pipeline Range?Cone?LOS, il posto giusto
    ///   è far sì che NeedsDecisionRule consulti Memory/ObjectPerception, non il World "nudo".
    /// </summary>
    public sealed class NeedsDecisionRule : IRule
    {
        // Throttle: decidiamo ogni N tick-pulse (per log leggibile)
        private readonly int _decisionEveryTicks;

        // Range di ricerca "decisionale" per cibo/letto.
        // È volutamente conservativo: evita che un NPC "usi" risorse che stanno a metà mappa
        // solo perché la LOS non è bloccata (corridoio lungo, ecc.).
        private readonly int _maxSeekRangeCells;

        public NeedsDecisionRule(int decisionEveryTicks = 10, int maxSeekRangeCells = 8)
        {
            _decisionEveryTicks = Mathf.Max(1, decisionEveryTicks);
            _maxSeekRangeCells = Mathf.Max(1, maxSeekRangeCells);
        }

        public void Handle(World world, ISimEvent e, List<ICommand> outCommands, Telemetry telemetry)
        {
            // Usiamo TickPulseEvent come clock decisionale.
            if (e is not TickPulseEvent pulse)
                return;

            if ((pulse.TickIndex % _decisionEveryTicks) != 0)
                return;

            var cfg = world.Global.Needs;

            int ate = 0, slept = 0, antisocial = 0;

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
                        if (didSteal) antisocial++;
                        continue; // una sola azione per tick
                    }
                }

                // --- DORMI ---
                if (needs.Fatigue01 >= cfg.tiredThreshold)
                {
                    if (TryPlanSleep(world, npcId, needs, out var cmd, out bool didTrespass))
                    {
                        outCommands.Add(cmd);
                        slept++;
                        if (didTrespass) antisocial++;
                        continue; // una sola azione per tick
                    }
                }
            }

            if (ate + slept + antisocial > 0)
            {
                ArcontioLogger.Info(
                new LogContext(tick: (int)TickContext.CurrentTickIndex, channel: "NeedsDecisionRule"),
                new LogBlock(LogLevel.Info, "log.needsconfig.Handle")
                    .AddField("tick=", pulse.TickIndex)
                    .AddField("ate==", ate)
                    .AddField("antisocial==", antisocial));
            }
        }

        // ============================================================
        // EAT DECISION
        // ============================================================

        private bool TryPlanEat(World world, int npcId, Needs needs, out ICommand cmd, out bool didSteal)
        {
            cmd = null;
            didSteal = false;

            // 1) privato
            if (world.NpcPrivateFood.TryGetValue(npcId, out int priv) && priv > 0)
            {
                cmd = new EatPrivateFoodCommand(npcId);
                return true;
            }

            // 2) stock community (VISIBILE)
            // Nota: qui sta il bug storico.
            // Prima bastava "Units>0". Questo permette telepatia (mangio cibo dietro un muro).
            // Ora filtriamo con range + LOS usando coordinate *corrette* dal World.
            int foodObj = FindVisibleCommunityFoodStock(world, npcId, _maxSeekRangeCells);
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

        /// <summary>
        /// Trova uno stock di cibo della Community che l'NPC può *realisticamente* usare:
        /// - lo stock deve avere Units > 0
        /// - deve essere OwnerKind=Community, OwnerId=0 (convenzione attuale)
        /// - deve essere "visibile" secondo un test minimo (range + LOS)
        ///
        /// Perché qui e non in World?
        /// - World è "verità oggettiva" e dovrebbe restare tendenzialmente neutro.
        /// - La nozione di "posso usarlo perché lo vedo" è una policy decisionale.
        /// </summary>
        private static int FindVisibleCommunityFoodStock(World world, int npcId, int maxRangeCells)
        {
            if (!TryGetNpcCell(world, npcId, out int nx, out int ny))
                return 0;

            foreach (var kv in world.FoodStocks)
            {
                int objId = kv.Key;
                var st = kv.Value;
                //if (st == null) continue;

                if (st.Units <= 0) continue;
                if (st.OwnerKind != OwnerKind.Community || st.OwnerId != 0) continue;

                // IMPORTANTISSIMO:
                // Le coordinate NON sono in FoodStockComponent.
                // Le coordinate stanno in world.Objects[objId].
                if (!TryGetObjectCell(world, objId, out int ox, out int oy))
                    continue;

                // Range discreto: Manhattan (cheap e coerente con grid).
                int manhattan = Mathf.Abs(ox - nx) + Mathf.Abs(oy - ny);
                if (manhattan > maxRangeCells)
                    continue;

                // LOS: se un muro è in mezzo, HasLineOfSight deve tornare false.
                if (!world.HasLineOfSight(nx, ny, ox, oy))
                    continue;

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

        // ============================================================
        // SLEEP DECISION
        // ============================================================

        private bool TryPlanSleep(World world, int npcId, Needs needs, out ICommand cmd, out bool didTrespass)
        {
            cmd = null;
            didTrespass = false;

            // 1) letto community libero (VISIBILE)
            int bedCommunity = FindVisibleBed(world, npcId, OwnerKind.Community, 0, _maxSeekRangeCells);
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

            int bedOther = FindAnyOwnedBedNotNpc(world, npcId, _maxSeekRangeCells);
            if (bedOther != 0 && !world.GetUseStateOrDefault(bedOther).IsInUse)
            {
                didTrespass = true;
                cmd = new SleepInBedCommand(npcId, bedOther, "Trespass");
                return true;
            }

            return false;
        }

        private static int FindVisibleBed(World world, int npcId, OwnerKind ownerKind, int ownerId, int maxRangeCells)
        {
            if (!TryGetNpcCell(world, npcId, out int nx, out int ny))
                return 0;

            foreach (var kv in world.Objects)
            {
                int objId = kv.Key;
                var obj = kv.Value;
                if (obj == null) continue;

                // v0: un letto se defId contiene "bed" (manteniamo la regola originale del file).
                if (string.IsNullOrWhiteSpace(obj.DefId)) continue;
                if (!obj.DefId.Contains("bed")) continue;

                if (obj.OwnerKind != ownerKind || obj.OwnerId != ownerId) continue;

                int ox = obj.CellX;
                int oy = obj.CellY;

                int manhattan = Mathf.Abs(ox - nx) + Mathf.Abs(oy - ny);
                if (manhattan > maxRangeCells)
                    continue;

                if (!world.HasLineOfSight(nx, ny, ox, oy))
                    continue;

                return objId;
            }

            return 0;
        }

        private static int FindAnyOwnedBedNotNpc(World world, int npcId, int maxRangeCells)
        {
            if (!TryGetNpcCell(world, npcId, out int nx, out int ny))
                return 0;

            foreach (var kv in world.Objects)
            {
                int objId = kv.Key;
                var obj = kv.Value;
                if (obj == null) continue;

                // v0: un letto se defId contiene "bed" (manteniamo la regola originale del file).
                if (string.IsNullOrWhiteSpace(obj.DefId)) continue;
                if (!obj.DefId.Contains("bed")) continue;

                // Escludi letti di proprietà dell'NPC.
                if (obj.OwnerKind == OwnerKind.Npc && obj.OwnerId == npcId) continue;

                int ox = obj.CellX;
                int oy = obj.CellY;

                int manhattan = Mathf.Abs(ox - nx) + Mathf.Abs(oy - ny);
                if (manhattan > maxRangeCells)
                    continue;

                if (!world.HasLineOfSight(nx, ny, ox, oy))
                    continue;

                return objId;
            }

            return 0;
        }

        // ============================================================
        // VERY SMALL UTILITIES (deliberatamente verbose)
        // ============================================================

        /// <summary>
        /// Estrae la cella corrente dell'NPC.
        /// In ARCONTIO la posizione runtime degli NPC è in world.GridPos (component store),
        /// NON dentro NpcCore (che è più "identità"/stato logico).
        /// </summary>
        private static bool TryGetNpcCell(World world, int npcId, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (!world.GridPos.TryGetValue(npcId, out var pos))
                return false;

            x = pos.X;
            y = pos.Y;
            return true;
        }

        /// <summary>
        /// Estrae la cella di un oggetto dato il suo objectId.
        /// Nota: questa è la *singola fonte di verità* per posizione oggetti in World:
        /// world.Objects[objId].CellX / CellY.
        ///
        /// (Il FoodStockComponent NON contiene necessariamente coordinate; è un componente logico.)
        /// </summary>
        private static bool TryGetObjectCell(World world, int objectId, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (!world.Objects.TryGetValue(objectId, out var obj) || obj == null)
                return false;

            x = obj.CellX;
            y = obj.CellY;
            return true;
        }
    }
}
