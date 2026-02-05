using System;

namespace Arcontio.Core
{
    /// <summary>
    /// TokenType: tassonomia minima dei messaggi simbolici.
    /// 
    /// Nota:
    /// - Cresce nel tempo.
    /// - Non è linguaggio naturale: sono categorie astratte.
    /// </summary>
    public enum TokenType
    {
        // Danger/Threat
        PredatorAlert,
        AlarmDanger,

        // Social request
        HelpRequest,

        // (futuro) DeathNotice, Accusation, ResourceHint, ecc.
    }

    /// <summary>
    /// SymbolicToken: contenuto informativo astratto.
    /// 
    /// "Cosa viene detto", non "come" e non "a chi".
    /// Questo rimane dentro TokenEnvelope.
    /// </summary>
    public readonly struct SymbolicToken
    {
        public readonly TokenType Type;

        // SubjectId: chi/che cosa è il focus del token
        // - predatorId per PredatorAlert
        // - attackerId per AlarmDanger (se vuoi)
        // - speakerId non va qui: sta nell'envelope
        public readonly int SubjectId;

        // Opzionale: localizzazione del contenuto ("qui")
        public readonly int CellX;
        public readonly int CellY;
        public readonly bool HasCell;

        // Intensità/urgenza percepita (0..1)
        public readonly float Intensity01;

        // Affidabilità del contenuto (0..1)
        public readonly float Reliability01;

        // Provenienza: quante "catene" ha già attraversato (rumor vs diretto)
        public readonly int ChainDepth;

        public SymbolicToken(
            TokenType type,
            int subjectId,
            float intensity01,
            float reliability01,
            int chainDepth = 0,
            bool hasCell = false,
            int cellX = 0,
            int cellY = 0)
        {
            Type = type;
            SubjectId = subjectId;
            Intensity01 = Clamp01(intensity01);
            Reliability01 = Clamp01(reliability01);
            ChainDepth = chainDepth < 0 ? 0 : chainDepth;

            HasCell = hasCell;
            CellX = cellX;
            CellY = cellY;
        }

        public override string ToString()
        {
            if (HasCell)
                return $"{Type} subj={SubjectId} int={Intensity01:0.00} rel={Reliability01:0.00} depth={ChainDepth} cell=({CellX},{CellY})";
            return $"{Type} subj={SubjectId} int={Intensity01:0.00} rel={Reliability01:0.00} depth={ChainDepth}";
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }

    /// <summary>
    /// TokenChannel: "come" è stato comunicato.
    /// </summary>
    public enum TokenChannel
    {
        ProximityTalk,
        AlarmShout,
        TargetedVisit
    }

    /// <summary>
    /// TokenEnvelope: contenitore di trasporto.
    /// 
    /// - SpeakerId: chi parla
    /// - ListenerId: chi ascolta (1:1 per ora)
    /// - Channel: come è stato trasmesso
    /// - Tick: quando è stato emesso
    /// - Token: contenuto simbolico
    /// </summary>
    public readonly struct TokenEnvelope
    {
        public readonly int SpeakerId;
        public readonly int ListenerId;
        public readonly TokenChannel Channel;
        public readonly long TickIndex;
        public readonly SymbolicToken Token;

        public TokenEnvelope(int speakerId, int listenerId, TokenChannel channel, long tickIndex, SymbolicToken token)
        {
            SpeakerId = speakerId;
            ListenerId = listenerId;
            Channel = channel;
            TickIndex = tickIndex;
            Token = token;
        }

        public override string ToString()
        {
            return $"env speaker={SpeakerId} listener={ListenerId} ch={Channel} tick={TickIndex} token=[{Token}]";
        }
    }
}
