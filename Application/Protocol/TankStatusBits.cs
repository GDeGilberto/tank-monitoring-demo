namespace Application.Protocol
{
    /// <summary>
    /// Bits del campo de estado de 4 hex (ssss) de cada tanque en la trama de inventario.
    /// Definidos por este proyecto (ver docs/protocolo.md). Un adaptador para hardware real
    /// debe traducir el estado nativo a estos bits.
    /// </summary>
    public static class TankStatusBits
    {
        /// <summary>Hay una entrega (descarga) en curso.</summary>
        public const int DeliveryInProgress = 0x0001;

        /// <summary>La altura no es valida (tanque casi vacio).</summary>
        public const int HeightInvalid = 0x0004;
    }
}
