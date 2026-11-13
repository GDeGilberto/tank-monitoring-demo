namespace Infrastructure.Simulation
{
    /// <summary>
    /// Generador SplitMix64. Se usa en lugar de System.Random porque garantiza la misma secuencia
    /// en cualquier version de .NET y plataforma (win-x86, linux-arm64).
    /// </summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;

        public DeterministicRandom(ulong seed) => _state = seed;

        public ulong NextUInt64()
        {
            ulong z = unchecked(_state += 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            return z ^ (z >> 31);
        }

        /// <summary>Numero en [0, 1).</summary>
        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        public double NextRange(double min, double max) => min + (max - min) * NextDouble();

        /// <summary>Normal estandar (Box-Muller) truncada a [-2, 2].</summary>
        public double NextTruncatedNormal()
        {
            double u1 = Math.Max(NextDouble(), 1e-12);
            double u2 = NextDouble();
            double n = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return Math.Clamp(n, -2.0, 2.0);
        }
    }
}
