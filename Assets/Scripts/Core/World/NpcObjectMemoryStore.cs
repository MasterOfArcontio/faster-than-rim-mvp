using System;

namespace Arcontio.Core
{
    /// <summary>
    /// Memoria "sparsa" a slot fissi per oggetti interagibili (Day10 baseline, ma la usiamo ora).
    ///
    /// Perché serve:
    /// - le MemoryTrace generiche sono ottime per “storie” (predatori, crimini, morte…)
    /// - per “conosco un letto / un cibo / un workbench” conviene una struttura ad-hoc,
    ///   più compatta e facile da queryare.
    /// </summary>
    public sealed class NpcObjectMemoryStore
    {
        public readonly Entry[] Slots;
        public readonly int Capacity;
        public int Count;

        public NpcObjectMemoryStore(int capacity)
        {
            Capacity = capacity < 1 ? 1 : capacity;
            Slots = new Entry[Capacity];
            Count = 0;
        }

        public struct Entry
        {
            public bool IsValid;

            // Identità “logica”
            public string DefId;
            public int ObjectId; // opzionale: se vuoi tracciare l'istanza

            // Dove
            public int CellX;
            public int CellY;

            // Ownership (utile per “è mio/non è mio”)
            public OwnerKind OwnerKind;
            public int OwnerId;

            // Recenza/affidabilità/utility
            public int LastSeenTick;
            public float Reliability01;
            public float UtilityScore01;

            // Pin: mai buttare se è “mio”
            public bool IsPinned;
        }

        /// <summary>
        /// Upsert:
        /// - se trova slot equivalente (stesso DefId + cella, oppure stesso ObjectId se usi quello), fa merge
        /// - altrimenti inserisce in slot libero o rimpiazza il “peggiore”
        /// </summary>
        public void Upsert(
            int nowTick,
            string defId,
            int objectId,
            int x, int y,
            OwnerKind ownerKind,
            int ownerId,
            float reliability01,
            float utility01,
            bool pinIfOwnedByNpc,
            int npcIdForPinLogic
        )
        {
            // 1) merge se già presente
            for (int i = 0; i < Slots.Length; i++)
            {
                if (!Slots[i].IsValid) continue;

                bool same =
                    (Slots[i].ObjectId != 0 && objectId != 0 && Slots[i].ObjectId == objectId) ||
                    (Slots[i].DefId == defId && Slots[i].CellX == x && Slots[i].CellY == y);

                if (!same) continue;

                var e = Slots[i];
                e.LastSeenTick = nowTick;

                // Merge “prendi la migliore”
                if (reliability01 > e.Reliability01) e.Reliability01 = reliability01;
                if (utility01 > e.UtilityScore01) e.UtilityScore01 = utility01;

                // Aggiorna owner (può cambiare)
                e.OwnerKind = ownerKind;
                e.OwnerId = ownerId;

                // Pin se è mio
                if (pinIfOwnedByNpc && ownerKind == OwnerKind.Npc && ownerId == npcIdForPinLogic)
                    e.IsPinned = true;

                Slots[i] = e;
                return;
            }

            // 2) inserisci in slot libero
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].IsValid) continue;

                Slots[i] = new Entry
                {
                    IsValid = true,
                    DefId = defId,
                    ObjectId = objectId,
                    CellX = x,
                    CellY = y,
                    OwnerKind = ownerKind,
                    OwnerId = ownerId,
                    LastSeenTick = nowTick,
                    Reliability01 = reliability01,
                    UtilityScore01 = utility01,
                    IsPinned = (pinIfOwnedByNpc && ownerKind == OwnerKind.Npc && ownerId == npcIdForPinLogic)
                };
                return;
            }

            // 3) store pieno: rimpiazza il peggiore NON pinned
            int worstIdx = -1;
            float worstScore = float.MaxValue;

            for (int i = 0; i < Slots.Length; i++)
            {
                var e = Slots[i];
                if (!e.IsValid) continue;
                if (e.IsPinned) continue; // mai rimpiazzare pinned

                // Metrica semplice: più vecchio e meno utile = peggio
                int age = nowTick - e.LastSeenTick;
                float score = (age * 0.01f) + (1f - e.UtilityScore01) + (1f - e.Reliability01);

                if (score < worstScore) continue;
                worstScore = score;
                worstIdx = i;
            }

            if (worstIdx < 0)
                return; // tutti pinned => non inseriamo

            Slots[worstIdx] = new Entry
            {
                IsValid = true,
                DefId = defId,
                ObjectId = objectId,
                CellX = x,
                CellY = y,
                OwnerKind = ownerKind,
                OwnerId = ownerId,
                LastSeenTick = nowTick,
                Reliability01 = reliability01,
                UtilityScore01 = utility01,
                IsPinned = (pinIfOwnedByNpc && ownerKind == OwnerKind.Npc && ownerId == npcIdForPinLogic)
            };
        }

        /// <summary>
        /// Cleanup:
        /// rimuove entries non-pinned troppo vecchie o inutili.
        /// (prima versione semplice: solo age threshold)
        /// </summary>
        public void Cleanup(int nowTick, int maxAgeTicks)
        {
            if (maxAgeTicks < 1) return;

            for (int i = 0; i < Slots.Length; i++)
            {
                var e = Slots[i];
                if (!e.IsValid) continue;
                if (e.IsPinned) continue;

                int age = nowTick - e.LastSeenTick;
                if (age > maxAgeTicks)
                    Slots[i].IsValid = false;
            }
        }
    }
}
