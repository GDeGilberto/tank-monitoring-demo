using Application.Interfaces;
using Application.Protocol;
using Application.UseCases;
using Infrastructure.Simulation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Test.Support;

namespace Test.Simulation
{
    public class TankGeometryTests
    {
        [Fact]
        public void Height_IsMonotonic_AndRoundTrips()
        {
            var g = new TankGeometry(30000, 2200);
            double previous = -1;
            for (double v = 0; v <= 30000; v += 500)
            {
                double h = g.HeightAtVolume(v);
                Assert.True(h >= previous);
                previous = h;
                Assert.Equal(v, g.VolumeAtHeight(h), 3);
            }
            Assert.Equal(2200, g.HeightAtVolume(30000), 3);
        }

        [Fact]
        public void HalfFull_IsHalfDiameter()
        {
            var g = new TankGeometry(30000, 2200);
            Assert.Equal(1100, g.HeightAtVolume(15000), 2);
        }
    }

    public class DeterministicRandomTests
    {
        [Fact]
        public void SameSeed_SameSequence_ByteForByte()
        {
            var a = new DeterministicRandom(42);
            var b = new DeterministicRandom(42);
            for (int i = 0; i < 100; i++) Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }

        [Fact]
        public void KnownFirstValues_AreStable_AcrossPlatforms()
        {
            // Valores de referencia de SplitMix64 con semilla 42 (estables entre versiones de .NET).
            var r = new DeterministicRandom(42);
            Assert.Equal(13679457532755275413UL, r.NextUInt64());
        }

        [Fact]
        public void NextDouble_IsInUnitInterval()
        {
            var r = new DeterministicRandom(1);
            for (int i = 0; i < 1000; i++)
            {
                double d = r.NextDouble();
                Assert.InRange(d, 0.0, 1.0);
                Assert.True(d < 1.0);
            }
        }
    }

    public class TankSimulatorTests
    {
        private static readonly DateTime T0 = new(2026, 11, 12, 6, 0, 0);

        private static string Fingerprint(int seed, int steps)
        {
            var sim = new TankSimulator(new SimulationOptions { Seed = seed });
            sim.Advance(T0);
            sim.Advance(T0.AddMinutes(steps));
            return string.Join("|", sim.Inspect().Select(t => $"{t.Number}:{t.Volume:R}:{t.Temperature:R}:{t.WaterMm:R}:{t.Deliveries}"));
        }

        [Fact]
        public void Seed42_100Steps_IsDeterministic()
        {
            Assert.Equal(Fingerprint(42, 100), Fingerprint(42, 100));
            Assert.NotEqual(Fingerprint(42, 100), Fingerprint(43, 100));
        }

        [Fact]
        public void CadenceOfReads_DoesNotChangeTheResult()
        {
            var a = new TankSimulator(new SimulationOptions());
            var b = new TankSimulator(new SimulationOptions());
            a.Advance(T0); b.Advance(T0);
            a.Advance(T0.AddMinutes(60));
            for (int m = 1; m <= 60; m++) b.Advance(T0.AddMinutes(m));
            Assert.Equal(a.Inspect(), b.Inspect());
        }

        [Fact]
        public void Advance_IsIdempotent_ForTheSameInstant()
        {
            var sim = new TankSimulator(new SimulationOptions());
            sim.Advance(T0);
            sim.Advance(T0.AddMinutes(10));
            var first = sim.Inspect();
            sim.Advance(T0.AddMinutes(10));
            sim.Advance(T0.AddMinutes(5)); // hacia atras: no cambia nada
            Assert.Equal(first, sim.Inspect());
        }

        [Fact]
        public void PhysicalLimits_AreNeverExceeded_OverADay()
        {
            var opts = new SimulationOptions { ConsumptionScale = 60 };
            var sim = new TankSimulator(opts);
            sim.Advance(T0);
            for (int m = 1; m <= 1440; m++)
            {
                sim.Advance(T0.AddMinutes(m));
                var report = sim.SnapshotInventory(T0.AddMinutes(m));
                foreach (var tank in report.Tanks!)
                {
                    var d = tank.TankData!;
                    var cap = opts.EffectiveTanks.Single(t => t.Number == tank.NoTank).CapacityLiters;
                    Assert.InRange(d.Volume, 0f, (float)cap + 0.01f);
                    Assert.InRange(d.Temperature, 5f, 40f);
                    Assert.True(d.Water >= 0 && d.Water <= 15.01);
                    Assert.True(d.WaterVolume <= d.Volume + 0.001f);
                    Assert.True(Math.Abs((cap - d.Volume) - d.Ullage!.Value) < 0.5, "ullage = capacidad - volumen");
                }
            }
        }

        [Fact]
        public void SmallTank_WithHugeConsumption_ReachesZero_NeverNegative()
        {
            var opts = new SimulationOptions
            {
                ConsumptionScale = 1,
                DemoMode = false,
                Tanks = [new TankOptions { Number = 1, Code = 'A', CapacityLiters = 1000, DiameterMm = 1200, InitialPercent = 0.5, BaseLitersPerHour = 6000 }],
                Delivery = new DeliveryOptions { IntervalMinutes = 100000, FirstAfterMinutes = 100000, LowLevelPercent = 0 },
            };
            var sim = new TankSimulator(opts);
            sim.Advance(T0);
            sim.Advance(T0.AddMinutes(30));
            var tank = sim.Inspect().Single();
            Assert.Equal(0, tank.Volume);
            var snapshot = sim.SnapshotInventory(T0.AddMinutes(30)).Tanks![0];
            Assert.NotEqual(0, snapshot.StatusTank & TankStatusBits.HeightInvalid);
        }

        [Fact]
        public void CatchUp_IsCappedAt24Hours()
        {
            var sim = new TankSimulator(new SimulationOptions());
            sim.Advance(T0);
            sim.Advance(T0.AddDays(30)); // no debe tardar ni desbordar
            Assert.All(sim.Inspect(), t => Assert.InRange(t.Volume, 0, 40000));
        }

        [Fact]
        public void Delivery_SetsFlagWhileInProgress_ThenRecordsEnt()
        {
            var opts = new SimulationOptions();
            var sim = new TankSimulator(opts);
            sim.Advance(T0);
            bool sawFlag = false;
            for (int m = 1; m <= 30; m++)
            {
                var now = T0.AddMinutes(m);
                sim.Advance(now);
                if (sim.SnapshotInventory(now).Tanks!.Any(t => (t.StatusTank & TankStatusBits.DeliveryInProgress) != 0))
                    sawFlag = true;
            }
            Assert.True(sawFlag, "debe verse la bandera de entrega en curso");

            var deliveries = sim.SnapshotDeliveries(T0.AddMinutes(30));
            var any = deliveries.Tanks!.SelectMany(t => t.Deliveries).ToList();
            Assert.NotEmpty(any);
            Assert.All(any, d =>
            {
                Assert.True(d.End.Date >= d.Start.Date);
                Assert.True(d.End.Volume > d.Start.Volume);
                Assert.InRange(d.End.Volume - d.Start.Volume, 1f, 8001f);
            });
        }

        [Fact]
        public void Delivery_IsClippedToFreeSpace_AtNinetyNinePercent()
        {
            var opts = new SimulationOptions
            {
                DemoMode = false,
                Tanks = [new TankOptions { Number = 1, Code = 'A', CapacityLiters = 10000, DiameterMm = 2000, InitialPercent = 99, BaseLitersPerHour = 0 }],
                Delivery = new DeliveryOptions { FirstAfterMinutes = 1, IntervalMinutes = 5 },
            };
            var sim = new TankSimulator(opts);
            sim.Advance(T0);
            sim.Advance(T0.AddMinutes(20));
            var tank = sim.Inspect().Single();
            Assert.True(tank.Volume <= 10000.001);
            // Con 100 L libres (>= 100) a lo sumo hubo una entrega recortada; nunca excede la capacidad.
            Assert.True(tank.Deliveries <= 4);
        }

        [Fact]
        public void Delivery_IsPostponed_WhenFreeSpaceBelow100Liters()
        {
            var opts = new SimulationOptions
            {
                DemoMode = false,
                Tanks = [new TankOptions { Number = 1, Code = 'A', CapacityLiters = 10000, DiameterMm = 2000, InitialPercent = 99.95, BaseLitersPerHour = 0 }],
            };
            var sim = new TankSimulator(opts);
            sim.Advance(T0);
            sim.Advance(T0.AddMinutes(60));
            Assert.Equal(0, sim.Inspect().Single().Deliveries);
        }

        [Fact]
        public void DemoMode_ConsumptionIsVisible_InThreeMinutes()
        {
            var sim = new TankSimulator(new SimulationOptions { Delivery = new DeliveryOptions { FirstAfterMinutes = 1000, IntervalMinutes = 1000, LowLevelPercent = 0 } });
            sim.Advance(T0);
            var before = sim.Inspect().Single(t => t.Number == 1).Volume;
            sim.Advance(T0.AddMinutes(3));
            var after = sim.Inspect().Single(t => t.Number == 1).Volume;
            Assert.True(before - after > 10, $"se esperaba un cambio visible, fue {before - after:F2} L");
        }
    }

    public class SimulatedTankReaderTests
    {
        private static (SimulatedTankReader reader, FakeTimeProvider time) Create()
        {
            var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 8, 0, 0, TimeSpan.Zero));
            time.SetLocalTimeZone(TimeZoneInfo.Utc);
            var reader = new SimulatedTankReader(Options.Create(new SimulationOptions()), time, NullLogger<SimulatedTankReader>.Instance);
            return (reader, time);
        }

        [Fact]
        public async Task Inv000_ReturnsFourTanks_ThatRoundTripWithTheParser()
        {
            var (reader, time) = Create();
            time.Advance(TimeSpan.FromMinutes(5));

            var result = await reader.SendCommandAsync("INV000", TimeSpan.FromSeconds(10));

            Assert.Equal(TankReadStatus.Ok, result.Status);
            var report = new ParseTankInventoryReport().Execute(result.Frame!);
            Assert.Equal(4, report.Tanks!.Count);
            Assert.Equal(new[] { 'A', 'B', 'C', 'C' }, report.Tanks.Select(t => t.CodeProduct).ToArray());
        }

        [Fact]
        public async Task UnknownCommand_ReturnsFramedErr0()
        {
            var (reader, _) = Create();
            var result = await reader.SendCommandAsync("XXX000", TimeSpan.FromSeconds(1));
            Assert.Equal("\u0001ERR0FEE6\u0003", result.Frame);
            var ex = Assert.Throws<TankFrameException>(() => new ParseTankInventoryReport().Execute(result.Frame!));
            Assert.Equal(TankFrameError.UnknownCommand, ex.Error);
        }

        [Fact]
        public async Task SingleTankCommand_ReturnsOnlyThatTank()
        {
            var (reader, _) = Create();
            var result = await reader.SendCommandAsync("INV003", TimeSpan.FromSeconds(1));
            var report = new ParseTankInventoryReport().Execute(result.Frame!);
            Assert.Equal(3, Assert.Single(report.Tanks!).NoTank);
        }

        [Fact]
        public async Task Ent000_ReturnsDeliveries_AfterEnoughSimulatedTime()
        {
            var (reader, time) = Create();
            await reader.SendCommandAsync("INV000", TimeSpan.FromSeconds(1));
            time.Advance(TimeSpan.FromMinutes(15));
            var result = await reader.SendCommandAsync("ENT000", TimeSpan.FromSeconds(1));
            var report = new ParceDeliveryReport().Execute(result.Frame!);
            Assert.Equal(4, report.Tanks!.Count);
            Assert.Contains(report.Tanks, t => t.Deliveries.Count > 0);
        }
    }
}
