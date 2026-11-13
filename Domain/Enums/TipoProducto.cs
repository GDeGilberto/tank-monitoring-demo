namespace Domain.Enums
{
    /// <summary>
    /// Tipo de liquido almacenado en un tanque. Los tipos son genericos (A, B y C).
    /// </summary>
    public enum TipoProducto
    {
        Desconocido = 0,
        TipoA = 1,
        TipoB = 2,
        TipoC = 3,
    }

    public static class TipoProductoExtensions
    {
        /// <summary>Texto legible para la interfaz ("Tipo A").</summary>
        public static string ToDisplay(this TipoProducto tipo) => tipo switch
        {
            TipoProducto.TipoA => "Tipo A",
            TipoProducto.TipoB => "Tipo B",
            TipoProducto.TipoC => "Tipo C",
            _ => "Desconocido",
        };
    }
}
