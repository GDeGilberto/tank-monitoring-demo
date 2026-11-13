using Application.Interfaces;
using Infrastructure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Ports;
using System.Text;

namespace Infrastructure.Communication
{
    /// <summary>
    /// Lector real sobre puerto serial. Serializa los comandos con un semaforo, usa un tiempo de
    /// espera por inactividad para detectar el fin de la respuesta y cierra el puerto en cada lectura.
    /// Implementa <see cref="ITankReader"/> (jobs) e <see cref="ISerialPortService"/> (consola).
    /// </summary>
    public class SerialPortManager : ISerialPortService, ITankReader
    {
        private SerialPort? _serialPort;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly StringBuilder _receivedDataBuffer = new();
        private TaskCompletionSource<string>? _responseTcs;
        private Timer? _inactivityTimer;
        private const int InactivityTimeout = 300; // ms
        private bool _initialized = false;
        private bool _disposed = false;
        private readonly SerialPortSettings _settings;
        private readonly ILogger<SerialPortManager> _logger;

        public SerialPortManager(IOptions<SerialPortSettings> settings, ILogger<SerialPortManager> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public event EventHandler<string>? DataReceived;
        public event EventHandler<string>? CompleteResponseReceived;

        public bool IsOpen => _serialPort != null && _serialPort.IsOpen;

        /// <inheritdoc />
        public async Task<TankReadResult> SendCommandAsync(string command, TimeSpan timeout, CancellationToken ct = default)
        {
            try
            {
                string response = await SendCommandAsync(_settings, command, (int)timeout.TotalMilliseconds, ct);
                return TankReadResult.Success(response);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Tiempo de espera agotado en la comunicacion serial ({Timeout} s)", timeout.TotalSeconds);
                return TankReadResult.TimedOut();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error en la comunicacion serial");
                return TankReadResult.Failed(ex.Message);
            }
        }

        public async Task<string> SendCommandAsync(SerialPortSettings settings, string command, int timeoutMs = 3000, CancellationToken ct = default)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                Initialize(settings);

                _responseTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                Write(command);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs);
                using var registration = cts.Token.Register(() => _responseTcs.TrySetCanceled(), useSynchronizationContext: false);

                return await _responseTcs.Task;
            }
            finally
            {
                Close();
                _semaphore.Release();
            }
        }

        public void Initialize(string portName, int baudRate)
        {
            Initialize(new SerialPortSettings { PortName = portName, BaudRate = baudRate });
        }

        public void Initialize(SerialPortSettings settings)
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
                _initialized = false;
                _disposed = false;
            }

            _logger.LogInformation("Inicializando puerto {Port} a {BaudRate} baudios", settings.PortName, settings.BaudRate);

            _serialPort = new SerialPort(settings.PortName, settings.BaudRate)
            {
                DataBits = settings.DataBits,
                Parity = settings.GetParity(),
                StopBits = settings.GetStopBits(),
                Handshake = settings.GetHandshake(),
                ReadTimeout = settings.ReadTimeout,
                WriteTimeout = settings.WriteTimeout
            };

            _serialPort.DataReceived += SerialPort_DataReceived;
            _inactivityTimer = new Timer(InactivityTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
            _serialPort.Open();
            _initialized = true;

            _logger.LogInformation("Puerto {Port} inicializado: {Parity}, {DataBits} bits de datos, {StopBits}, handshake {Handshake}",
                settings.PortName, _serialPort.Parity, _serialPort.DataBits, _serialPort.StopBits, _serialPort.Handshake);
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string newData = _serialPort!.ReadExisting();
                if (!string.IsNullOrEmpty(newData))
                {
                    _inactivityTimer?.Change(InactivityTimeout, Timeout.Infinite);
                    _receivedDataBuffer.Append(newData);
                    DataReceived?.Invoke(this, newData);

                    if (newData.Contains('\u0003'))
                    {
                        ProcessCompleteResponse();
                    }
                }
            }
            catch (Exception ex)
            {
                _responseTcs?.TrySetException(ex);
            }
        }

        private void InactivityTimerElapsed(object? state)
        {
            if (_receivedDataBuffer.Length > 0)
            {
                ProcessCompleteResponse();
            }
        }

        private void ProcessCompleteResponse()
        {
            _inactivityTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            string completeResponse = _receivedDataBuffer.ToString();
            _receivedDataBuffer.Clear();
            CompleteResponseReceived?.Invoke(this, completeResponse);
            _responseTcs?.TrySetResult(completeResponse);
        }

        public void Write(string data)
        {
            if (!_initialized || _serialPort == null)
                throw new InvalidOperationException("El puerto serial no ha sido inicializado. Llame a Initialize() primero.");

            if (!_serialPort.IsOpen)
                throw new InvalidOperationException("El puerto serial no esta abierto");

            string framedData = $"\x01{data}";
            _serialPort.Write(framedData);
        }

        public void Close()
        {
            if (!_disposed)
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }
                _inactivityTimer?.Dispose();
                _serialPort?.Dispose();
                _disposed = true;
            }
        }

        public void Dispose() => Close();

        public void ClearBuffer()
        {
            _receivedDataBuffer.Clear();
        }
    }
}
