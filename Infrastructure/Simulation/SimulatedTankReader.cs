using Application.Interfaces;
using Application.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Infrastructure.Simulation
{
    /// <summary>
    /// Lector simulado: implementa <see cref="ITankReader"/> con el protocolo propio. Responde INV000 y
    /// ENT000 con tramas del <see cref="TankFrameBuilder"/> (checksum valido) y ERR0 a cualquier otro comando.
    /// No exige codigo de seguridad ni abre ningun puerto.
    /// </summary>
    public sealed class SimulatedTankReader : ITankReader
    {
        private readonly TankSimulator _simulator;
        private readonly SimulationOptions _options;
        private readonly TimeProvider _time;
        private readonly ILogger<SimulatedTankReader> _logger;

        public SimulatedTankReader(IOptions<SimulationOptions> options, TimeProvider time, ILogger<SimulatedTankReader> logger)
        {
            _options = options.Value;
            _time = time;
            _logger = logger;
            _simulator = new TankSimulator(_options);
            // Primer avance: fija el instante inicial del reloj simulado.
            _simulator.Advance(_time.GetLocalNow().DateTime);
        }

        public async Task<TankReadResult> SendCommandAsync(string command, TimeSpan timeout, CancellationToken ct = default)
        {
            if (_options.ResponseDelayMs > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(_options.ResponseDelayMs), _time, ct);

            DateTime now = _time.GetLocalNow().DateTime;
            _simulator.Advance(now);

            string cmd = command.Trim(TankFrame.Soh, TankFrame.Etx, ' ');
            string frame = BuildResponse(cmd, now);
            return TankReadResult.Success(frame);
        }

        private string BuildResponse(string cmd, DateTime now)
        {
            if (cmd.Length != TankFrame.HeaderLength || cmd[3] != '0')
                return TankFrameBuilder.BuildUnknownCommand();

            string code = cmd[..3].ToUpperInvariant();
            if (!int.TryParse(cmd[4..], NumberStyles.None, CultureInfo.InvariantCulture, out int tank))
                return TankFrameBuilder.BuildUnknownCommand();
            int? selected = tank == 0 ? null : tank;

            switch (code)
            {
                case "INV":
                    return TankFrameBuilder.BuildInventory(cmd, _simulator.SnapshotInventory(now, selected));
                case "ENT":
                    return TankFrameBuilder.BuildDeliveries(cmd, _simulator.SnapshotDeliveries(now, selected));
                default:
                    _logger.LogDebug("Comando no soportado por el simulador: {Command}", code);
                    return TankFrameBuilder.BuildUnknownCommand();
            }
        }
    }
}
