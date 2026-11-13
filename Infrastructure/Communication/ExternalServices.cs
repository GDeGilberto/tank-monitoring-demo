using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Json;

namespace Infrastructure.Communication
{
    /// <summary>Objeto nulo: no abre ninguna conexion de red (ExternalService:Enabled=false).</summary>
    public sealed class NullExternalService : IExternalService
    {
        public Task<bool> PublishInventoryAsync(InventarioEntity inventario, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    /// <summary>
    /// Envia cada inventario como JSON a un endpoint HTTP configurable
    /// (ExternalService:Endpoint). Las credenciales, si las hay, vienen de variables de entorno.
    /// </summary>
    public sealed class ExternalHttpService : IExternalService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ExternalHttpService> _logger;
        private readonly string _endpoint;

        public ExternalHttpService(HttpClient httpClient, IConfiguration configuration, ILogger<ExternalHttpService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _endpoint = configuration["ExternalService:Endpoint"]
                ?? throw new InvalidOperationException("ExternalService:Endpoint no configurado");

            string? token = configuration["ExternalService:Token"];
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Add("X-Api-Key", token);
        }

        public async Task<bool> PublishInventoryAsync(InventarioEntity inventario, CancellationToken ct = default)
        {
            try
            {
                var payload = new
                {
                    sitio = inventario.IdSitio,
                    tanque = inventario.NoTanque,
                    tipo = inventario.ClaveProducto,
                    fecha = inventario.Fecha?.ToString("s", CultureInfo.InvariantCulture),
                    volumen = inventario.VolumenDisponible,
                    temperatura = inventario.Temperatura,
                };

                var response = await _httpClient.PostAsJsonAsync(_endpoint, payload, ct);
                if (response.IsSuccessStatusCode) return true;

                _logger.LogWarning("El servicio externo respondio {Status}", (int)response.StatusCode);
                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "No se pudo enviar el inventario al servicio externo");
                return false;
            }
        }
    }
}
