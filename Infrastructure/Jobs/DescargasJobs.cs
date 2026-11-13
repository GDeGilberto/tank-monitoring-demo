using Application.Interfaces;
using Application.Options;
using Application.Protocol;
using Application.Services;
using Application.UseCases;
using Domain.Entities;
using Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Jobs
{
    /// <summary>Lee las entregas (descargas) de todos los tanques (comando ENT000) y guarda todas las nuevas.</summary>
    public class DescargasJobs
    {
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(40);

        private readonly ITankReader _reader;
        private readonly ParceDeliveryReport _parser;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DescargasJobs> _logger;
        private readonly IOptions<SiteOptions> _site;

        public DescargasJobs(
            ITankReader reader,
            ParceDeliveryReport parser,
            IServiceScopeFactory scopeFactory,
            ILogger<DescargasJobs> logger,
            IOptions<SiteOptions> site)
        {
            _reader = reader;
            _parser = parser;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _site = site;
        }

        public async Task Execute()
        {
            int idSitio = _site.Value.Id;
            if (idSitio <= 0)
            {
                _logger.LogError("Sitio:Id invalido: {Id}", idSitio);
                return;
            }

            TankReadResult read = await _reader.SendCommandAsync(TankFrameBuilder.DeliveriesCommand, ReadTimeout);
            if (read.Status != TankReadStatus.Ok)
            {
                throw new InvalidOperationException($"Lectura de descargas fallida: {read.Status} {read.Error}");
            }

            DeliveryTankReport report;
            try
            {
                report = _parser.Execute(read.Frame!);
            }
            catch (TankFrameException ex)
            {
                _logger.LogWarning("Trama de descargas rechazada ({Error}): {Message}", ex.Error, ex.Message);
                return;
            }

            if (report.Tanks is null || report.Tanks.Count == 0)
            {
                _logger.LogWarning("La trama de descargas no contiene tanques");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var descargasService = scope.ServiceProvider.GetRequiredService<DescargasService<DescargasEntity>>();

            int saved = 0, duplicated = 0, failed = 0;

            foreach (DeliveryTank tank in report.Tanks)
            {
                foreach (FullDeliveries delivery in tank.Deliveries)
                {
                    try
                    {
                        var descarga = new DescargasEntity(
                            idSitio,
                            tank.NoTank,
                            delivery.Start.Volume,
                            delivery.Start.Temperature,
                            delivery.Start.Date,
                            delivery.End.Volume,
                            delivery.End.Temperature,
                            delivery.End.Date,
                            delivery.End.Volume - delivery.Start.Volume);

                        await descargasService.AddAsync(descarga);
                        saved++;
                    }
                    catch (DuplicateEntityException)
                    {
                        duplicated++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, "Error al guardar descarga del tanque {NoTanque}", tank.NoTank);
                    }
                }
            }

            _logger.LogInformation("Descargas: {Saved} guardadas, {Dup} duplicadas, {Failed} errores", saved, duplicated, failed);
        }
    }
}
