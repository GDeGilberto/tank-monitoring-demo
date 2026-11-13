using Application.Interfaces;
using Application.Options;
using Infrastructure;
using Infrastructure.Data;
using Infrastructure.Jobs;
using Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Application.UseCases;

namespace Test.Jobs
{
    /// <summary>Extremo a extremo: modo Simulated + DemoMode, reloj falso y EF InMemory. Sin Task.Delay.</summary>
    public class DemoE2ETests
    {
        [Fact]
        public async Task FiveSimulatedMinutes_ProduceDifferentVolumes_AndAtLeastOneDelivery()
        {
            var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 6, 0, 0, TimeSpan.Zero));
            time.SetLocalTimeZone(TimeZoneInfo.Utc);

            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reader:Mode"] = "Simulated",
                ["Sitio:Id"] = "1",
                ["Simulation:DemoMode"] = "true",
            }).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            string db = Guid.NewGuid().ToString();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(db));
            services.AddSingleton<TimeProvider>(time);
            services.AddTankReader(config);
            services.AddExternalService(config);
            services.AddMonitoringData();
            services.AddScoped<ParseTankInventoryReport>();
            services.AddScoped<ParceDeliveryReport>();
            services.AddScoped<InventarioJob>();
            services.AddScoped<DescargasJobs>();
            await using var sp = services.BuildServiceProvider();

            await DemoDatabaseInitializer.InitializeAsync(sp);

            for (int minute = 0; minute <= 5; minute++)
            {
                using var scope = sp.CreateScope();
                await scope.ServiceProvider.GetRequiredService<InventarioJob>().Execute();
                await scope.ServiceProvider.GetRequiredService<DescargasJobs>().Execute();
                time.Advance(TimeSpan.FromMinutes(1));
            }

            using var check = sp.CreateScope();
            var context = check.ServiceProvider.GetRequiredService<AppDbContext>();

            var perTank = context.ProcInventarios.AsEnumerable().GroupBy(i => i.NoTanque).ToList();
            Assert.Equal(4, perTank.Count);
            Assert.All(perTank, g => Assert.True(g.Select(i => i.VolumenDisponible).Distinct().Count() > 1,
                $"el tanque {g.Key} debe cambiar de volumen"));

            var simulated = context.ProcDescargas.AsEnumerable().Where(d => d.FechaInicio >= new DateTime(2026, 11, 12, 6, 0, 0)).ToList();
            Assert.NotEmpty(simulated);
        }

        [Fact]
        public async Task SeedIsIdempotent_AndCreatesFourTanks_WithGenericTypes()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reader:Mode"] = "Simulated",
                ["Sitio:Id"] = "1",
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            string dbName = Guid.NewGuid().ToString();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddTankReader(config);
            await using var sp = services.BuildServiceProvider();

            await DemoDatabaseInitializer.InitializeAsync(sp);
            await DemoDatabaseInitializer.InitializeAsync(sp);

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Single(db.CatSitios);
            Assert.Equal(new[] { "TipoA", "TipoB", "TipoC", "TipoC" }, db.CatTanques.OrderBy(t => t.NoTanque).Select(t => t.Producto).ToArray());
            Assert.NotEmpty(db.ProcDescargas);
        }
    }
}
