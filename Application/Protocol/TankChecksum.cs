namespace Application.Protocol
{
    /// <summary>
    /// Checksum del protocolo propio: complemento a 2 (modulo 65536) de la suma de los
    /// caracteres de la trama, desde SOH hasta los dos "&amp;&amp;" inclusive. 4 hex en mayusculas.
    /// </summary>
    public static class TankChecksum
    {
        public static string Compute(ReadOnlySpan<char> frameThroughTerminator)
        {
            int sum = 0;
            foreach (char c in frameThroughTerminator) sum += c;
            int checksum = (65536 - (sum % 65536)) % 65536;
            return checksum.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
