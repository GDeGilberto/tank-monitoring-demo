using System.Globalization;
using Application.Interfaces;
using Application.Options;
using Application.Protocol;
using Application.Services;
using Application.UseCases;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Jobs;
using Infrastructure.Models;
using Infrastructure.Repositories;
using Infrastructure.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Test.Support
{
    /// <summary>Checksum ingenuo propio (D7): no comparte codigo con produccion.</summary>
    public static class NaiveChecksum
    {
        public static string Of(string throughTerminator)
        {
            int sum = 0;
            foreach (char c in throughTerminator) sum += c;
            return ((-sum) & 0xFFFF).ToString("X4", CultureInfo.InvariantCulture);
        }

        /// <summary>Arma SOH + cuerpo + "&amp;&amp;" + checksum + ETX con el checksum ingenuo.</summary>
        public static string Frame(string headerAndBody)
        {
            string head = "\u0001" + headerAndBody + "&&";
            return head + Of(head) + "\u0003";
        }

        public static string Hex(float value) =>
            BitConverter.SingleToInt32Bits(value).ToString("X8", CultureInfo.InvariantCulture);
    }

    /// <summary>Lector falso con respuestas programadas.</summary>
    public sealed class FakeTankReader : ITankReader
    {
        private readonly Func<string, TankReadResult> _responder;
        public List<string> Commands { get; } = [];

        public FakeTankReader(Func<string, TankReadResult> responder) => _responder = responder;

        public static FakeTankReader Returning(string frame) => new(_ => TankReadResult.Success(frame));
        public static FakeTankReader Timeout() => new(_ => TankReadResult.TimedOut());

        public Task<TankReadResult> SendCommandAsync(string command, TimeSpan timeout, CancellationToken ct = default)
        {
            Commands.Add(command);
            return Task.FromResult(_responder(command));
        }
    }

    public static class TestDb
    {
        public const int SiteId = 7;

        /// <summary>Contenedor de servicios con EF InMemory (una base nueva por prueba) y sitio con 4 tanques.</summary>
        public static ServiceProvider CreateServices(ITankReader reader, TimeProvider? time = null, bool seed = true)
        {
            var services = new ServiceCollection();
            string dbName = Guid.NewGuid().ToString();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddLogging();
            services.AddSingleton(time ?? TimeProvider.System);
            services.AddSingleton(reader);
            services.AddSingleton<IOptions<SiteOptions>>(Options.Create(new SiteOptions { Id = SiteId }));
            services.AddSingleton<IExternalService, Infrastructure.Communication.NullExternalService>();
            Infrastructure.DependencyInjection.AddMonitoringData(services);
            services.AddScoped<ParseTankInventoryReport>();
            services.AddScoped<ParceDeliveryReport>();
            services.AddScoped<InventarioJob>();
            services.AddScoped<DescargasJobs>();
            var provider = services.BuildServiceProvider();
            if (seed) Seed(provider);
            return provider;
        }

        public static void Seed(IServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CatSitios.Add(new CatSitiosModel { IdSitio = SiteId, Nombre = "Sitio de prueba" });
            string[] types = ["TipoA", "TipoB", "TipoC", "TipoC"];
            for (int i = 1; i <= 4; i++)
                db.CatTanques.Add(new CatTanqueModel { IdSitio = SiteId, NoTanque = i.ToString("D2"), Producto = types[i - 1], Capacidad = 30000 });
            db.SaveChanges();
        }

        public static T Get<T>(this ServiceProvider provider, Func<AppDbContext, T> query)
        {
            using var scope = provider.CreateScope();
            return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }
    }

    public static class Frames
    {
        /// <summary>Informe de inventario de ejemplo con tanques de volumen conocido.</summary>
        public static TankReport Inventory(DateTime date, params (int no, char code, float volume, float temp)[] tanks) => new()
        {
            Date = date,
            Tanks = tanks.Select(t => new Tank
            {
                NoTank = t.no,
                CodeProduct = t.code,
                StatusTank = 0,
                NumberDataHex = 7,
                TankData = new TankData { Volume = t.volume, TCVolume = t.volume, Ullage = 100, Height = 500, Water = 1, Temperature = t.temp, WaterVolume = 5 },
            }).ToList(),
        };

        public static FullDeliveries Delivery(DateTime start, float v0, float v1) => new()
        {
            Start = new DeliveryTankData { Date = start, Volume = v0, TCVolume = v0, Water = 1, Temperature = 20, Height = 400 },
            End = new DeliveryTankData { Date = start.AddMinutes(2), Volume = v1, TCVolume = v1, Water = 1.2f, Temperature = 21, Height = 900 },
        };

        public static DeliveryTankReport Deliveries(DateTime date, int tank, params FullDeliveries[] deliveries) => new()
        {
            Date = date,
            Tanks = [new DeliveryTank { NoTank = tank, CodeProduct = 'A', CountDeliveries = deliveries.Length, Deliveries = deliveries.ToList() }],
        };
    }
}
