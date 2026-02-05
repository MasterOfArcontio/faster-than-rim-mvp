using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// HelpRequestEmissionRule:
    /// Se uno speaker ha una trace di tipo AttackSuffered (o simili),
    /// può emettere un token "HelpRequest" verso un listener in prossimità.
    ///
    /// Nota importante (nuovo design):
    /// - NON costruiamo più TokenEnvelope con 8 argomenti (vecchio modello).
    /// - Ora TokenEnvelope è uno struct che contiene:
    ///   SpeakerId, ListenerId, Channel, TickIndex, SymbolicToken Token
    /// </summary>
    public sealed class HelpRequestEmissionRule : ITokenEmissionRule
    {
        public bool Matches(in MemoryTrace trace)
        {
            // Versione minima roadmap: da AttackSuffered -> HelpRequest
            return trace.Type == MemoryType.AttackSuffered;
        }

        public bool TryCreateToken(
            World world,
            long tickIndex,
            int speakerNpcId,
            int listenerNpcId,
            in MemoryTrace trace,
            out TokenEnvelope token)
        {
            // TokenEnvelope è struct: non può essere null.
            token = default;

            // Safety: non emettiamo se trace troppo debole
            // (valore tarabile; serve a evitare spam di memorie quasi scariche)
            if (trace.Intensity01 < 0.20f)
                return false;

            // Costruiamo il "contenuto" del messaggio: SymbolicToken
            // - Tipo: HelpRequest
            // - SubjectId: lo speaker stesso (chi chiede aiuto)
            // - Intensity: ereditata dalla trace (quanto è urgente)
            // - Reliability: ereditata dalla trace (quanto è affidabile)
            // - Cell: posizione dell'evento (se disponibile)
            var symbolic = new SymbolicToken(
                type: TokenType.HelpRequest,
                subjectId: speakerNpcId,
                intensity01: trace.Intensity01,
                reliability01: trace.Reliability01,
                chainDepth: 0,
                hasCell: true,
                cellX: trace.CellX,
                cellY: trace.CellY
            );

            // Ora costruiamo l'envelope (trasporto):
            // - Channel: per ora ProximityTalk (parlano perché sono vicini)
            // - TickIndex: lo prendiamo dal world / tick?
            //
            // IMPORTANTE: questa interfaccia NON riceve Tick.
            // Quindi abbiamo due opzioni:
            // (A) aggiungere tick all'interfaccia TryCreateToken
            // (B) usare un tick "corrente" salvato in world/global (quick hack)
            //
            // Per rimanere compatibili col tuo codice attuale, usiamo world.Global.TickIndex se esiste,
            // altrimenti mettiamo 0 e il SimulationHost può sovrascrivere.
            //long tickIndex = world.Global.TickIndex;

            token = new TokenEnvelope(
                speakerId: speakerNpcId,
                listenerId: listenerNpcId,
                //channel: TokenChannel.ProximityTalk,
                channel: TokenChannel.AlarmShout,
                tickIndex: tickIndex,
                token: symbolic
            );

            return true;
        }
    }
}
