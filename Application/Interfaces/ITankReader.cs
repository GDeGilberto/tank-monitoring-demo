namespace Application.Interfaces
{
    public enum TankReadStatus { Ok, Timeout, PortError }

    /// <summary>Resultado tipado de enviar un comando al lector.</summary>
    public sealed record TankReadResult(TankReadStatus Status, string? Frame = null, string? Error = null)
    {
        public static TankReadResult Success(string frame) => new(TankReadStatus.Ok, frame);
        public static TankReadResult TimedOut() => new(TankReadStatus.Timeout, null, "Tiempo de espera agotado");
        public static TankReadResult Failed(string error) => new(TankReadStatus.PortError, null, error);
    }

    /// <summary>
    /// Abstraccion del hardware de medicion: puerto serial real o simulador.
    /// El comando se envia sin los caracteres de control; la respuesta es la trama completa.
    /// </summary>
    public interface ITankReader
    {
        Task<TankReadResult> SendCommandAsync(string command, TimeSpan timeout, CancellationToken ct = default);
    }
}
