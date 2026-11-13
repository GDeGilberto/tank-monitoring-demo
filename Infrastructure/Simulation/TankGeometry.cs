namespace Infrastructure.Simulation
{
    /// <summary>Cilindro horizontal de capacidad y diametro dados. Volumen en litros, alturas en mm.</summary>
    public sealed class TankGeometry
    {
        private readonly double _radiusMm;
        private readonly double _lengthMm;

        public double CapacityLiters { get; }
        public double DiameterMm { get; }

        public TankGeometry(double capacityLiters, double diameterMm)
        {
            CapacityLiters = capacityLiters;
            DiameterMm = diameterMm;
            _radiusMm = diameterMm / 2.0;
            _lengthMm = capacityLiters * 1_000_000.0 / (Math.PI * _radiusMm * _radiusMm);
        }

        /// <summary>Volumen (litros) para una altura de liquido h (mm).</summary>
        public double VolumeAtHeight(double heightMm)
        {
            double h = Math.Clamp(heightMm, 0, DiameterMm);
            double r = _radiusMm;
            double a = Math.Acos(Math.Clamp((r - h) / r, -1, 1));
            double segment = r * r * a - (r - h) * Math.Sqrt(Math.Max(0, 2 * r * h - h * h));
            return Math.Clamp(_lengthMm * segment / 1_000_000.0, 0, CapacityLiters);
        }

        /// <summary>Altura (mm) para un volumen (litros), por biseccion (monotona).</summary>
        public double HeightAtVolume(double liters)
        {
            double v = Math.Clamp(liters, 0, CapacityLiters);
            double lo = 0, hi = DiameterMm;
            for (int i = 0; i < 40; i++)
            {
                double mid = (lo + hi) / 2;
                if (VolumeAtHeight(mid) < v) lo = mid; else hi = mid;
            }
            return (lo + hi) / 2;
        }
    }
}
