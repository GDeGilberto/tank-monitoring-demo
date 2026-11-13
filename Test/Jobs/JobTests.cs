using Application.Interfaces;
using Application.Options;
using Application.Protocol;
using Domain.Entities;
using Infrastructure;
using Infrastructure.Communication;
using Infrastructure.Jobs;
using Infrastructure.Models;
using Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Test.Support;

namespace Test.Jobs
{
    public class InventarioJobTests
    {
        private static readonly DateTime Date = new(2026, 11, 12, 9, 30, 0);

        private static async Task Run(ServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            await scope.ServiceProvider.GetRequiredService<InventarioJob>().Execute();
        }

        [Fact]
        public async Task OkFrame_SavesOneRowPerTank_WithTheTypeFromTheCatalog()
        {
            string frame = TankFrameBuilder.BuildInventory("INV000", Frames.Inventory(Date,
                (1, 'A', 1000f, 20f), (2, 'B', 2000f, 21f), (3, 'C', 3000f, 22f), (4, 'C', 4000f, 23f)));
            using var sp = TestDb.CreateServices(FakeTankReader.Returning(frame));

            await Run(sp);

            var rows = sp.Get(db => db.ProcInventarios.OrderBy(i => i.NoTanque).ToList());
            Assert.Equal(4, rows.Count);
            Assert.Equal(new[] { "TipoA", "TipoB", "TipoC", "TipoC" }, rows.Select(r => r.ClaveProducto).ToArray());
            Assert.Equal(1000d, rows[0].VolumenDisponible);
            Assert.All(rows, r => Assert.Equal(TestDb.SiteId, r.Idsitio));
        }

        [Fact]
        public async Task Timeout_Throws_AndSavesNothing()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(sp));
            Assert.Equal(0, sp.Get(db => db.ProcInventarios.Count()));
        }

        [Fact]
        public async Task AlteredChecksum_IsRejected_WithoutSaving()
        {
            string frame = TankFrameBuilder.BuildInventory("INV000", Frames.Inventory(Date, (1, 'A', 1000f, 20f)));
            string bad = frame.Replace("INV000", "INV001");
            using var sp = TestDb.CreateServices(FakeTankReader.Returning(bad));

            await Run(sp); // no lanza: se registra como advertencia

            Assert.Equal(0, sp.Get(db => db.ProcInventarios.Count()));
        }

        [Fact]
        public async Task Err0Frame_IsRejected_WithoutSaving()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Returning(TankFrameBuilder.BuildUnknownCommand()));
            await Run(sp);
            Assert.Equal(0, sp.Get(db => db.ProcInventarios.Count()));
        }

        [Fact]
        public async Task SameMinuteTwice_DoesNotDuplicate()
        {
            string frame = TankFrameBuilder.BuildInventory("INV000", Frames.Inventory(Date, (1, 'A', 1000f, 20f)));
            var time = new FakeTimeProvider(new DateTimeOffset(Date, TimeSpan.Zero));
            time.SetLocalTimeZone(TimeZoneInfo.Utc);
            using var sp = TestDb.CreateServices(FakeTankReader.Returning(frame), time);

            await Run(sp);
            await Run(sp);

            Assert.Equal(1, sp.Get(db => db.ProcInventarios.Count()));
        }

        [Fact]
        public async Task UsesTheInjectedClock_NotDateTimeNow()
        {
            string frame = TankFrameBuilder.BuildInventory("INV000", Frames.Inventory(Date, (1, 'A', 1000f, 20f)));
            var time = new FakeTimeProvider(new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
            time.SetLocalTimeZone(TimeZoneInfo.Utc);
            using var sp = TestDb.CreateServices(FakeTankReader.Returning(frame), time);

            await Run(sp);

            Assert.Equal(new DateTime(2030, 1, 2, 3, 4, 5), sp.Get(db => db.ProcInventarios.Single().Fecha));
        }

        [Fact]
        public async Task SendsTheInventoryCommand()
        {
            var reader = FakeTankReader.Returning(TankFrameBuilder.BuildUnknownCommand());
            using var sp = TestDb.CreateServices(reader);
            await Run(sp);
            Assert.Equal("INV000", Assert.Single(reader.Commands));
        }
    }

    public class DescargasJobTests
    {
        private static async Task Run(ServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            await scope.ServiceProvider.GetRequiredService<DescargasJobs>().Execute();
        }

        [Fact]
        public async Task ThreeDeliveriesInOneTank_SavesThree()
        {
            var report = Frames.Deliveries(new DateTime(2026, 11, 12, 12, 0, 0), 1,
                Frames.Delivery(new DateTime(2026, 11, 12, 6, 0, 0), 1000, 5000),
                Frames.Delivery(new DateTime(2026, 11, 12, 8, 0, 0), 2000, 6000),
                Frames.Delivery(new DateTime(2026, 11, 12, 10, 0, 0), 3000, 7000));
            using var sp = TestDb.CreateServices(FakeTankReader.Returning(TankFrameBuilder.BuildDeliveries("ENT000", report)));

            await Run(sp);

            var rows = sp.Get(db => db.ProcDescargas.OrderBy(d => d.FechaInicio).ToList());
            Assert.Equal(3, rows.Count);
            Assert.Equal(4000, rows[0].CantidadCargada);
        }

        [Fact]
        public async Task RunningTwice_DoesNotDuplicate()
        {
            var report = Frames.Deliveries(new DateTime(2026, 11, 12, 12, 0, 0), 1,
                Frames.Delivery(new DateTime(2026, 11, 12, 6, 0, 0), 1000, 5000));
            using var sp = TestDb.CreateServices(FakeTankReader.Returning(TankFrameBuilder.BuildDeliveries("ENT000", report)));

            await Run(sp);
            await Run(sp);

            Assert.Equal(1, sp.Get(db => db.ProcDescargas.Count()));
        }

        [Fact]
        public async Task Timeout_Throws()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Timeout());
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(sp));
        }

        [Fact]
        public async Task Err0_IsRejected_WithoutSaving()
        {
            using var sp = TestDb.CreateServices(FakeTankReader.Returning(TankFrameBuilder.BuildUnknownCommand()));
            await Run(sp);
            Assert.Equal(0, sp.Get(db => db.ProcDescargas.Count()));
        }
    }

    public class ReaderModeTests
    {
        private static ServiceProvider Build(string mode)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reader:Mode"] = mode,
                ["Sitio:Id"] = "1",
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddTankReader(config);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void SimulatedMode_ResolvesSimulatedTankReader_AndNoSerialManager()
        {
            using var sp = Build("Simulated");
            Assert.IsType<SimulatedTankReader>(sp.GetRequiredService<ITankReader>());
            Assert.Null(sp.GetService<SerialPortManager>());
            Assert.Null(sp.GetService<ISerialPortService>());
        }

        [Fact]
        public void SerialMode_ResolvesSerialPortManager_SameInstanceForBothInterfaces()
        {
            using var sp = Build("Serial");
            var reader = sp.GetRequiredService<ITankReader>();
            Assert.IsType<SerialPortManager>(reader);
            Assert.Same(reader, sp.GetRequiredService<ISerialPortService>());
        }

        [Fact]
        public void DefaultMode_IsSerial()
        {
            var config = new ConfigurationBuilder().Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddTankReader(config);
            using var sp = services.BuildServiceProvider();
            Assert.IsType<SerialPortManager>(sp.GetRequiredService<ITankReader>());
        }

        [Fact]
        public void SiteId_MustBePositive()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Reader:Mode"] = "Simulated",
                ["Sitio:Id"] = "0",
            }).Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(config);
            services.AddTankReader(config);
            using var sp = services.BuildServiceProvider();
            var ex = Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<SiteOptions>>().Value);
            Assert.Contains("Sitio:Id", ex.Message);
        }
    }

    public class SerialSettingsValidatorTests
    {
        [Fact]
        public void BaudRateNotANumber_FailsNamingTheKey()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SerialPort:BaudRate"] = "abc",
            }).Build();
            var services = new ServiceCollection();
            services.AddOptions<SerialPortSettings>().BindConfiguration("SerialPort");
            services.AddSingleton<IConfiguration>(config);
            using var sp = services.BuildServiceProvider();

            // El binder no puede convertir "abc": la configuracion invalida se detecta al leer las opciones.
            Assert.ThrowsAny<Exception>(() => sp.GetRequiredService<IOptions<SerialPortSettings>>().Value);
        }

        [Theory]
        [InlineData(0, "SerialPort:BaudRate")]
        [InlineData(-5, "SerialPort:BaudRate")]
        public void NonPositiveBaudRate_FailsNamingTheKey(int baud, string key)
        {
            var result = new SerialSettingsValidator().Validate(null, new SerialPortSettings { BaudRate = baud });
            Assert.True(result.Failed);
            Assert.Contains(result.Failures!, f => f.Contains(key));
        }

        [Fact]
        public void Defaults_AreValid()
        {
            Assert.True(new SerialSettingsValidator().Validate(null, new SerialPortSettings()).Succeeded);
        }

        [Fact]
        public void EmptyPortName_Fails()
        {
            var result = new SerialSettingsValidator().Validate(null, new SerialPortSettings { PortName = " " });
            Assert.Contains(result.Failures!, f => f.Contains("SerialPort:PortName"));
        }
    }
}
