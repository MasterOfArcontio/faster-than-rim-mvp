namespace Arcontio.Core
{
    /// <summary>
    /// System: logiche meccaniche di basso livello che scorrono lo stato.
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// Frequenza di aggiornamento: 1 = ogni tick, 5 = ogni 5 tick, ecc.
        /// (usato dallo Scheduler)
        /// </summary>
        int Period { get; }

        void Update(World world, Tick tick, MessageBus bus, Telemetry telemetry);
    }
}
