using Application.Protocol;
using Domain.Entities;

namespace Infrastructure.Simulation
{
    public enum DeliveryPhase { Pending, InProgress, Completed }

    internal sealed record DeliveryRecord(
        DateTime StartDate, double StartVolume, double StartTcVolume, double StartWater, double StartTemperature, double StartHeight,
        DateTime EndDate, double EndVolume, double EndTcVolume, double EndWater, double EndTemperature, double EndHeight);

    internal sealed class TankState
    {
        public required TankOptions Options { get; init; }
        public required TankGeometry Geometry { get; init; }
        public required DeterministicRandom Rng { get; init; }
        public double Volume;
        public double Temperature;
        public double WaterMm;
        public DeliveryPhase Phase = DeliveryPhase.Pending;
        public DateTime NextDeliveryAt;
        public double RemainingLiters;
        public DateTime DeliveryStart;
        public double StartVolume, StartTc, StartWater, StartTemperature, StartHeight;
        public List<DeliveryRecord> History { get; } = [];
    }

    /// <summary>
    /// Modelo fisico determinista de varios tanques cilindricos horizontales. Avanza en pasos fijos
    /// de 1 minuto de reloj simulado de forma perezosa (al leer), con un tope de 24 h de recuperacion.
    /// Es idempotente para el mismo instante y seguro entre hilos (un lock).
    /// </summary>
    public sealed class TankSimulator
    {
        private const double ReferenceTemperature = 15.56;
        private const double MaxWaterMm = 15;
        private const int MaxHistory = 10;
        private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MaxCatchUp = TimeSpan.FromHours(24);

        private static readonly double ProfileNormalization = ComputeProfileMean();

        private readonly object _lock = new();
        private readonly SimulationOptions _options;
        private readonly List<TankState> _tanks = [];
        private DateTime _last;
        private bool _initialized;

        public TankSimulator(SimulationOptions options)
        {
            _options = options;
            int index = 0;
            foreach (TankOptions t in options.EffectiveTanks)
            {
                var geometry = new TankGeometry(t.CapacityLiters, t.DiameterMm);
                ulong seed = unchecked((ulong)options.Seed * 0x9E3779B97F4A7C15UL + (ulong)t.Number * 0xD1B54A32D192ED03UL);
                var state = new TankState
                {
                    Options = t,
                    Geometry = geometry,
                    Rng = new DeterministicRandom(seed),
                    Volume = t.CapacityLiters * t.InitialPercent / 100.0,
                    Temperature = t.BaseTemperatureC,
                    WaterMm = 2 + index * 0.5,
                };
                _tanks.Add(state);
                index++;
            }
        }

        /// <summary>Avanza el modelo hasta <paramref name="now"/> (se trunca al minuto).</summary>
        public void Advance(DateTime now)
        {
            now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind);
            lock (_lock)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    _last = now;
                    for (int i = 0; i < _tanks.Count; i++)
                    {
                        double offset = _options.Delivery.FirstAfterMinutes + i * 1.5;
                        _tanks[i].NextDeliveryAt = now.AddMinutes(Math.Ceiling(offset));
                    }
                    return;
                }

                if (now <= _last) return;

                DateTime from = _last;
                if (now - from > MaxCatchUp) from = now - MaxCatchUp;

                for (DateTime t = from + Step; t <= now; t += Step)
                {
                    foreach (TankState tank in _tanks) StepTank(tank, t);
                }
                _last = now;
            }
        }

        /// <summary>Estado actual para la trama de inventario (INV).</summary>
        public TankReport SnapshotInventory(DateTime now, int? tankNumber = null)
        {
            lock (_lock)
            {
                var tanks = new List<Tank>();
                foreach (TankState s in _tanks)
                {
                    if (tankNumber is not null && s.Options.Number != tankNumber) continue;
                    double height = s.Geometry.HeightAtVolume(s.Volume);
                    int status = 0;
                    if (s.Phase == DeliveryPhase.InProgress) status |= TankStatusBits.DeliveryInProgress;
                    if (s.Volume < 1) { status |= TankStatusBits.HeightInvalid; height = 0; }

                    double waterVolume = Math.Min(s.Geometry.VolumeAtHeight(s.WaterMm), s.Volume);
                    tanks.Add(new Tank
                    {
                        NoTank = s.Options.Number,
                        CodeProduct = s.Options.Code,
                        StatusTank = status,
                        NumberDataHex = 7,
                        TankData = new TankData
                        {
                            Volume = (float)s.Volume,
                            TCVolume = (float)CompensatedVolume(s, s.Volume, s.Temperature),
                            Ullage = (float)(s.Geometry.CapacityLiters - s.Volume),
                            Height = (float)height,
                            Water = (float)s.WaterMm,
                            Temperature = (float)s.Temperature,
                            WaterVolume = (float)waterVolume,
                        },
                    });
                }
                return new TankReport { Date = now, Tanks = tanks };
            }
        }

        /// <summary>Entregas registradas por tanque para la trama ENT.</summary>
        public DeliveryTankReport SnapshotDeliveries(DateTime now, int? tankNumber = null)
        {
            lock (_lock)
            {
                var tanks = new List<DeliveryTank>();
                foreach (TankState s in _tanks)
                {
                    if (tankNumber is not null && s.Options.Number != tankNumber) continue;
                    var deliveries = s.History.Select(r => new FullDeliveries
                    {
                        Start = new DeliveryTankData
                        {
                            Date = r.StartDate, Volume = (float)r.StartVolume, TCVolume = (float)r.StartTcVolume,
                            Water = (float)r.StartWater, Temperature = (float)r.StartTemperature, Height = (float)r.StartHeight,
                        },
                        End = new DeliveryTankData
                        {
                            Date = r.EndDate, Volume = (float)r.EndVolume, TCVolume = (float)r.EndTcVolume,
                            Water = (float)r.EndWater, Temperature = (float)r.EndTemperature, Height = (float)r.EndHeight,
                        },
                    }).ToList();

                    tanks.Add(new DeliveryTank
                    {
                        NoTank = s.Options.Number,
                        CodeProduct = s.Options.Code,
                        CountDeliveries = deliveries.Count,
                        Deliveries = deliveries,
                    });
                }
                return new DeliveryTankReport { Date = now, Tanks = tanks };
            }
        }

        /// <summary>Estado interno para pruebas: volumen, temperatura y fase por tanque.</summary>
        public IReadOnlyList<(int Number, double Volume, double Temperature, double WaterMm, DeliveryPhase Phase, int Deliveries)> Inspect()
        {
            lock (_lock)
            {
                return _tanks.Select(s => (s.Options.Number, s.Volume, s.Temperature, s.WaterMm, s.Phase, s.History.Count)).ToList();
            }
        }

        private void StepTank(TankState s, DateTime t)
        {
            TankOptions o = s.Options;
            double hour = t.Hour + t.Minute / 60.0;

            // Consumo (perfil horario con dos picos, ruido truncado)
            double scale = _options.DemoMode ? _options.ConsumptionScale : 1.0;
            double noise = s.Rng.NextTruncatedNormal();
            double consumption = o.BaseLitersPerHour / 60.0 * Profile(hour) * scale * (1 + 0.25 * noise);
            consumption = Math.Clamp(consumption, 0, s.Volume);
            s.Volume -= consumption;

            // Temperatura: Ornstein-Uhlenbeck alrededor de una base con ciclo diario
            double baseTemp = o.BaseTemperatureC + 2 * Math.Sin(2 * Math.PI * (hour - 9) / 24.0);
            s.Temperature += 0.02 * (baseTemp - s.Temperature) + 0.05 * s.Rng.NextTruncatedNormal();
            s.Temperature = Math.Clamp(s.Temperature, _options.TemperatureMinC, _options.TemperatureMaxC);

            // Agua: crece lento con tope
            s.WaterMm = Math.Min(MaxWaterMm, s.WaterMm + o.WaterRateMmPerDay / 1440.0);

            StepDelivery(s, t);
        }

        private void StepDelivery(TankState s, DateTime t)
        {
            DeliveryOptions d = _options.Delivery;
            double capacity = s.Geometry.CapacityLiters;

            if (s.Phase == DeliveryPhase.Completed)
            {
                s.Phase = DeliveryPhase.Pending;
            }

            if (s.Phase == DeliveryPhase.Pending)
            {
                bool due = t >= s.NextDeliveryAt || s.Volume < capacity * d.LowLevelPercent / 100.0;
                if (!due) return;

                double free = capacity - s.Volume;
                if (free < 100)
                {
                    s.NextDeliveryAt = t.AddMinutes(d.IntervalMinutes);
                    return;
                }

                double amount = Math.Min(s.Rng.NextRange(d.MinLiters, d.MaxLiters), free);
                s.RemainingLiters = amount;
                s.Phase = DeliveryPhase.InProgress;
                s.DeliveryStart = t;
                s.StartVolume = s.Volume;
                s.StartTc = CompensatedVolume(s, s.Volume, s.Temperature);
                s.StartWater = s.WaterMm;
                s.StartTemperature = s.Temperature;
                s.StartHeight = s.Geometry.HeightAtVolume(s.Volume);
                return;
            }

            // InProgress
            double add = Math.Min(d.FlowLitersPerMinute, s.RemainingLiters);
            add = Math.Min(add, capacity - s.Volume);
            double deliveryTemp = s.Options.BaseTemperatureC + 3;
            double previous = s.Volume;
            s.Volume += add;
            if (s.Volume > 0)
                s.Temperature = (s.Temperature * previous + deliveryTemp * add) / s.Volume;
            s.RemainingLiters -= add;

            if (s.RemainingLiters <= 0.001 || s.Volume >= capacity - 0.001)
            {
                s.WaterMm = Math.Min(MaxWaterMm, s.WaterMm + 0.2);
                s.History.Add(new DeliveryRecord(
                    s.DeliveryStart, s.StartVolume, s.StartTc, s.StartWater, s.StartTemperature, s.StartHeight,
                    t, s.Volume, CompensatedVolume(s, s.Volume, s.Temperature), s.WaterMm, s.Temperature,
                    s.Geometry.HeightAtVolume(s.Volume)));
                if (s.History.Count > MaxHistory) s.History.RemoveAt(0);
                s.RemainingLiters = 0;
                s.Phase = DeliveryPhase.Completed;
                s.NextDeliveryAt = t.AddMinutes(d.IntervalMinutes);
            }
        }

        private static double CompensatedVolume(TankState s, double volume, double temperature) =>
            volume * (1 - s.Options.Beta * (temperature - ReferenceTemperature));

        // Perfil horario: 0.25 + 0.75 * (dos picos gaussianos a las 8 y 18 h), normalizado a media 1.
        private static double RawProfile(double hour)
        {
            static double Gauss(double h, double mu, double sigma) => Math.Exp(-0.5 * Math.Pow((h - mu) / sigma, 2));
            return 0.25 + 0.75 * (Gauss(hour, 8, 2) + Gauss(hour, 18, 2));
        }

        private static double Profile(double hour) => RawProfile(hour) / ProfileNormalization;

        private static double ComputeProfileMean()
        {
            double sum = 0;
            for (int m = 0; m < 1440; m++) sum += RawProfile(m / 60.0);
            return sum / 1440.0;
        }
    }
}
