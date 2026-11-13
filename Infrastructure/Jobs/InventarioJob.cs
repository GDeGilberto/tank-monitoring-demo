using Application.Interfaces;
using Application.Options;
using Application.Protocol;
using Application.Services;
using Application.UseCases;
using Domain.Entities;
using Infrastructure.Repositories;
using Infrastructure.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Jobs
{
    /// <summary>Lee el inventario de todos los tanques (comando INV000) y lo guarda.</summary>
    public class InventarioJob
    {
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

        private readonly ITankReader _reader;
        private readonly ParseTankInventoryReport _parser;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<InventarioJob> _logger;
        private readonly IExternalService _externalService;
        private readonly IOptions<SiteOptions> _site;
        private readonly TimeProvider _time;

        public InventarioJob(
            ITankReader reader,
            ParseTankInventoryReport parser,
            IServiceScopeFactory scopeFactory,
            ILogger<InventarioJob> logger,
            IExternalService externalService,
            IOptions<SiteOptions> site,
            TimeProvider time)
        {
            _reader = reader;
            _parser = parser;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _externalService = externalService;
            _site = site;
            _time = time;
        }

        public async Task Execute()
        {
            int idSitio = _site.Value.Id;
            if (idSitio <= 0)
            {
                _logger.LogError("Sitio:Id invalido: {Id}", idSitio);
                return;
            }

            TankReadResult read = await _reader.SendCommandAsync(TankFrameBuilder.InventoryCommand, ReadTimeout);
            if (read.Status != TankReadStatus.Ok)
            {
                // Se lanza para que Hangfire reintente; no se guarda nada parcial.
                throw new InvalidOperationException($"Lectura de inventario fallida: {read.Status} {read.Error}");
            }

            TankReport report;
            try
            {
                report = _parser.Execute(read.Frame!);
            }
            catch (TankFrameException ex)
            {
                _logger.LogWarning("Trama de inventario rechazada ({Error}): {Message}", ex.Error, ex.Message);
                return;
            }

            if (report.Tanks is null || report.Tanks.Count == 0)
            {
                _logger.LogWarning("La trama de inventario no contiene tanques");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var inventarioService = scope.ServiceProvider.GetRequiredService<InventarioService<InventarioEntity, InventarioViewModel>>();
            var getTanque = scope.ServiceProvider.GetRequiredService<GetTanqueBySitioAndNumeroUseCase>();

            DateTime now = _time.GetLocalNow().DateTime;
            int saved = 0, duplicated = 0, failed = 0;

            foreach (Tank tank in report.Tanks)
            {
                try
                {
                    var tanqueInfo = await getTanque.ExecuteAsync(idSitio, tank.NoTank);
                    string clave = tanqueInfo?.Producto ?? "";
                    if (clave.Length == 0)
                        _logger.LogWarning("Tanque {NoTanque} sin tipo asignado en el sitio {IdSitio}", tank.NoTank, idSitio);

                    var inventario = new InventarioEntity(
                        idSitio, tank.NoTank, clave,
                        tank.TankData?.Volume, tank.TankData?.Temperature, now);

                    await inventarioService.AddAsync(inventario);
                    saved++;

                    if (!await _externalService.PublishInventoryAsync(inventario))
                        _logger.LogDebug("El servicio externo no acepto el inventario del tanque {NoTanque}", tank.NoTank);
                }
                catch (DuplicateEntityException)
                {
                    duplicated++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Error al guardar inventario del tanque {NoTanque}", tank.NoTank);
                }
            }

            _logger.LogInformation("Inventario: {Total} tanques, {Saved} guardados, {Dup} duplicados, {Failed} errores",
                report.Tanks.Count, saved, duplicated, failed);
        }
    }
}
