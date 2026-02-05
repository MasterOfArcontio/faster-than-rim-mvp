using System.Collections.Generic;
using UnityEngine;

namespace Arcontio.Core
{
    /// <summary>
    /// TokenAssimilationPipeline (Giorno 6):
    /// 
    /// Scopo:
    /// - drena TokenBus (TokenEnvelope emessi da TokenEmission)
    /// - per ogni envelope trova una regola compatibile
    /// - aggiunge una traccia "heard/rumor" nel MemoryStore del listener
    /// 
    /// Nota:
    /// - NON fa delivery fisica (LOS, distanza, perdita): arriverà al Giorno 7.
    /// - Qui assumiamo che i token nel bus siano già "destinati" al listener.
    /// </summary>
    public sealed class TokenAssimilationPipeline
    {
        private readonly List<ITokenAssimilationRule> _rules = new();

        public TokenAssimilationPipeline()
        {
            // Catalogo minimo Giorno 6
            _rules.Add(new AssimilatePredatorAlertRule());
            _rules.Add(new AssimilateHelpRequestRule());
        }

        /// <summary>
        /// Assimilate:
        /// - drena dal TokenBus in un buffer esterno (riusabile)
        /// - processa tutti gli envelope
        /// - aggiorna MemoryStore dei listener
        /// </summary>
        public void Assimilate(World world, Tick tick, TokenBus tokenBus, List<TokenEnvelope> tokenBuffer, Telemetry telemetry)
        {
            // 1) Drain token bus
            tokenBus.DrainTo(tokenBuffer);

            if (tokenBuffer.Count == 0)
                return;

            int tracesAdded = 0;

            // 2) Per ogni envelope, applichiamo 1 regola (prima compatibile)
            for (int i = 0; i < tokenBuffer.Count; i++)
            {
                var env = tokenBuffer[i];

                // Safety: esiste store memoria?
                if (!world.Memory.TryGetValue(env.ListenerId, out var store) || store == null)
                    continue;

                bool handled = false;

                for (int r = 0; r < _rules.Count; r++)
                {
                    var rule = _rules[r];
                    if (!rule.Matches(env))
                        continue;

                    // Questa rule è quella "giusta" per questo token (anche se decide di non produrre trace)
                    handled = true;

                    if (rule.TryAssimilate(world, env, out var trace))
                    {
                        store.AddOrMerge(trace);
                        tracesAdded++;
                        
                        string hk = trace.IsHeard ? trace.HeardKind.ToString() : "Direct";
                        int cd = env.Token.ChainDepth;

                        UnityEngine.Debug.Log(
                            $"[TokenAssim] listener={env.ListenerId} speaker={env.SpeakerId} " +
                            $"token={env.Token.Type} subj={env.Token.SubjectId} chain={cd} rel={env.Token.Reliability01:0.00} " +
                            $"-> trace={trace.Type} heard={hk} rel={trace.Reliability01:0.00} int={trace.Intensity01:0.00}");
                    }

                    break;
                }

                if (!handled)
                {
                    telemetry.Counter($"TokenAssimilation.Unhandled.{env.Token.Type}", 1);
                    UnityEngine.Debug.Log($"[TokenAssim] UNHANDLED token: {env}");
                }
            }

            telemetry.Counter("TokenAssimilation.TracesAdded", tracesAdded);
        }

    }
}
