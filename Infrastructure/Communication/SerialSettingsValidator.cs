using Infrastructure.Models;
using Microsoft.Extensions.Options;

namespace Infrastructure.Communication
{
    /// <summary>Valida la seccion "SerialPort" y nombra la clave que falla.</summary>
    public class SerialSettingsValidator : IValidateOptions<SerialPortSettings>
    {
        private static readonly string[] ParityValues = ["none", "odd", "even", "mark", "space"];
        private static readonly string[] StopBitsValues = ["none", "one", "onepointfive", "two"];
        private static readonly string[] HandshakeValues = ["none", "xonxoff", "requesttosend", "requesttosendxonxoff"];

        public ValidateOptionsResult Validate(string? name, SerialPortSettings options)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(options.PortName))
                errors.Add("SerialPort:PortName es obligatorio.");
            if (options.BaudRate <= 0)
                errors.Add("SerialPort:BaudRate debe ser un entero mayor que cero.");
            if (options.DataBits is < 5 or > 8)
                errors.Add("SerialPort:DataBits debe estar entre 5 y 8.");
            if (!ParityValues.Contains(options.Parity?.ToLowerInvariant()))
                errors.Add("SerialPort:Parity no es valido (None, Odd, Even, Mark, Space).");
            if (!StopBitsValues.Contains(options.StopBits?.ToLowerInvariant()))
                errors.Add("SerialPort:StopBits no es valido (None, One, OnePointFive, Two).");
            if (!HandshakeValues.Contains(options.Handshake?.ToLowerInvariant()))
                errors.Add("SerialPort:Handshake no es valido.");
            if (options.ReadTimeout <= 0)
                errors.Add("SerialPort:ReadTimeout debe ser mayor que cero.");
            if (options.WriteTimeout <= 0)
                errors.Add("SerialPort:WriteTimeout debe ser mayor que cero.");

            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }
    }
}
