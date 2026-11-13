namespace Infrastructure.Simulation
{
    /// <summary>Configuracion del simulador (seccion "Simulation").</summary>
    public class SimulationOptions
    {
        public const string SectionName = "Simulation";

        /// <summary>Semilla del generador pseudoaleatorio: misma semilla, mismos datos.</summary>
        public int Seed { get; set; } = 42;

        /// <summary>
        /// Modo demo: aplica <see cref="ConsumptionScale"/> para que el cambio sea visible en minutos.
        /// </summary>
        public bool DemoMode { get; set; } = true;

        /// <summary>Multiplicador del consumo (solo si DemoMode es true).</summary>
        public double ConsumptionScale { get; set; } = 30;

        /// <summary>Retardo artificial de cada respuesta, en milisegundos.</summary>
        public int ResponseDelayMs { get; set; } = 0;

        public double TemperatureMinC { get; set; } = 5;
        public double TemperatureMaxC { get; set; } = 40;

        public DeliveryOptions Delivery { get; set; } = new();

        /// <summary>Tanques simulados. Si esta vacio se usan los 4 tanques ilustrativos por defecto.</summary>
        public List<TankOptions> Tanks { get; set; } = [];

        public IReadOnlyList<TankOptions> EffectiveTanks => Tanks.Count > 0 ? Tanks : DefaultTanks;

        public static readonly IReadOnlyList<TankOptions> DefaultTanks =
        [
            new TankOptions { Number = 1, Code = 'A', CapacityLiters = 40000, DiameterMm = 2400, InitialPercent = 70, BaseLitersPerHour = 500, Beta = 0.0012, BaseTemperatureC = 24 },
            new TankOptions { Number = 2, Code = 'B', CapacityLiters = 20000, DiameterMm = 2000, InitialPercent = 55, BaseLitersPerHour = 200, Beta = 0.0012, BaseTemperatureC = 24 },
            new TankOptions { Number = 3, Code = 'C', CapacityLiters = 30000, DiameterMm = 2200, InitialPercent = 45, BaseLitersPerHour = 300, Beta = 0.0008, BaseTemperatureC = 25 },
            new TankOptions { Number = 4, Code = 'C', CapacityLiters = 30000, DiameterMm = 2200, InitialPercent = 80, BaseLitersPerHour = 150, Beta = 0.0008, BaseTemperatureC = 25 },
        ];
    }

    public class DeliveryOptions
    {
        /// <summary>Minutos hasta la primera descarga del primer tanque (los demas se escalonan 1.5 min).</summary>
        public int FirstAfterMinutes { get; set; } = 1;

        /// <summary>Minutos entre descargas programadas de un mismo tanque.</summary>
        public int IntervalMinutes { get; set; } = 10;

        public double MinLiters { get; set; } = 4000;
        public double MaxLiters { get; set; } = 8000;

        /// <summary>Caudal de descarga en litros por minuto.</summary>
        public double FlowLitersPerMinute { get; set; } = 3000;

        /// <summary>Si el tanque baja de este porcentaje se programa una descarga de inmediato.</summary>
        public double LowLevelPercent { get; set; } = 20;
    }

    public class TankOptions
    {
        public int Number { get; set; }

        /// <summary>Codigo del tipo de liquido en la trama: A, B o C.</summary>
        public char Code { get; set; } = 'A';

        public double CapacityLiters { get; set; } = 30000;
        public double DiameterMm { get; set; } = 2200;
        public double InitialPercent { get; set; } = 60;
        public double BaseLitersPerHour { get; set; } = 300;

        /// <summary>Coeficiente de dilatacion termica por grado C (volumen compensado).</summary>
        public double Beta { get; set; } = 0.001;

        public double BaseTemperatureC { get; set; } = 24;

        /// <summary>Crecimiento diario de la altura de agua, en milimetros.</summary>
        public double WaterRateMmPerDay { get; set; } = 0.3;
    }
}
