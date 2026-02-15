namespace Arcontio.Core
{
    /// <summary>
    /// FoodMissingEvent (Day9):
    /// Evento "deduttivo" prodotto da un audit:
    /// - la vittima nota che il suo cibo privato è diminuito rispetto all'ultima verifica,
    ///   e NON ha un motivo interno certo (v0: non tracciamo ancora tutte le cause).
    ///
    /// Questo NON è "verità del mondo del furto", è un indizio percepito/dedotto.
    /// Quindi diventa una memoria con Reliability bassa (sospetto).
    /// </summary>
    public readonly struct FoodMissingEvent : ISimEvent
    {
        public readonly int VictimNpcId;
        public readonly int MissingUnits;

        public FoodMissingEvent(int victimNpcId, int missingUnits)
        {
            VictimNpcId = victimNpcId;
            MissingUnits = missingUnits;
        }

        public override string ToString()
        {
            return $"FoodMissing victim={VictimNpcId} missingUnits={MissingUnits}";
        }
    }
}
