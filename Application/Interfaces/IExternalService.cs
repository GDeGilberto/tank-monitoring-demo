using Domain.Entities;

namespace Application.Interfaces
{
    /// <summary>
    /// Servicio externo opcional al que se envian los inventarios (por ejemplo un webhook).
    /// Cuando esta desactivado se usa una implementacion nula que no abre ninguna conexion.
    /// </summary>
    public interface IExternalService
    {
        Task<bool> PublishInventoryAsync(InventarioEntity inventario, CancellationToken ct = default);
    }
}
