namespace Domain.Entities;

public class InventarioEntity
{
    public int Id { get; }

    public int? IdSitio { get; }

    public int? NoTanque { get; }

    public string? ClaveProducto { get; }

    public double? VolumenDisponible { get; }

    public double? Temperatura { get; }

    public DateTime? Fecha { get; }

    public InventarioEntity(int? idSitio, int? noTanque, string? claveProducto, double? volumenDisponible, double? temperatura, DateTime? fecha)
    {
        IdSitio = idSitio;
        NoTanque = noTanque;
        ClaveProducto = claveProducto;
        VolumenDisponible = volumenDisponible;
        Temperatura = temperatura;
        Fecha = fecha;
    }
}
