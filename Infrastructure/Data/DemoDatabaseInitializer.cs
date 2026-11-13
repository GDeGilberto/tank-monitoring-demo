using Application.Options;
using Infrastructure.Models;
using Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Data
{
    /// <summary>
    /// Crea el esquema (EnsureCreated) y siembra el sitio ficticio, sus tanques y un historial de
    /// descargas de los ultimos dias. Reintenta mientras PostgreSQL termina de arrancar.
    /// </summary>
    public static class DemoDatabaseInitializer
    {
        public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDatabaseInitializer");
            var db = sp.GetRequiredService<AppDbContext>();
            var site = sp.GetRequiredService<IOptions<SiteOptions>>().Value;
            var time = sp.GetRequiredService<TimeProvider>();
            var simulation = sp.GetService<IOptions<SimulationOptions>>()?.Value ?? new SimulationOptions();

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await db.Database.EnsureCreatedAsync(ct);
                    break;
                }
                catch (Exception ex) when (attempt < 30 && !ct.IsCancellationRequested)
                {
                    logger.LogWarning("Base de datos no disponible todavia (intento {Attempt}): {Message}", attempt, ex.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
            }

            if (await db.CatSitios.AnyAsync(s => s.IdSitio == site.Id, ct))
                return;

            logger.LogInformation("Sembrando el sitio de demostracion {SiteId} y sus tanques", site.Id);

            var sitio = new CatSitiosModel
            {
                IdSitio = site.Id,
                Nombre = "Sitio de demostración",
                Direccion = "Datos simulados",
                Activa = "S",
            };
            db.CatSitios.Add(sitio);

            foreach (TankOptions t in simulation.EffectiveTanks)
            {
                db.CatTanques.Add(new CatTanqueModel
                {
                    IdSitio = site.Id,
                    NoTanque = t.Number.ToString("D2"),
                    Producto = $"Tipo{t.Code}",
                    Capacidad = (decimal)t.CapacityLiters,
                    Capacidad95 = (decimal)(t.CapacityLiters * 0.95),
                    Fondeja = 0,
                });
            }
            await db.SaveChangesAsync(ct);

            // Historial de descargas de los ultimos 7 dias (deterministico, sin datos reales).
            DateTime today = time.GetLocalNow().DateTime;
            var rng = new DeterministicRandom((ulong)simulation.Seed + 7);
            foreach (TankOptions t in simulation.EffectiveTanks)
            {
                int count = 3 + (int)(rng.NextDouble() * 3);
                for (int i = 0; i < count; i++)
                {
                    double daysAgo = 0.3 + (6.5 * (i + rng.NextDouble() * 0.8)) / count;
                    DateTime start = today.AddDays(-daysAgo);
                    start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0);
                    double amount = Math.Round(rng.NextRange(4000, 8000) / 10) * 10;
                    double volumeStart = Math.Round(t.CapacityLiters * rng.NextRange(0.12, 0.35));
                    db.ProcDescargas.Add(new ProcDescargaModel
                    {
                        IdSitio = site.Id,
                        NoTanque = t.Number,
                        VolumenInicio = volumeStart,
                        TemperaturaInicio = Math.Round(t.BaseTemperatureC + rng.NextRange(-2, 2), 1),
                        FechaInicio = start,
                        VolumenDisponible = volumeStart + amount,
                        TemperaturaFinal = Math.Round(t.BaseTemperatureC + rng.NextRange(0, 3), 1),
                        FechaFinal = start.AddMinutes(Math.Ceiling(amount / 3000.0) + 1),
                        CantidadCargada = amount,
                    });
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
