using Domain.Entities;

namespace Application.Interfaces
{
    public interface ITanqueRepository
    {
        Task<TanqueEntity?> GetTanqueBySitioAndNumeroAsync(int idSitio, int noTanque);
        Task<IEnumerable<TanqueEntity>> GetAllAsync();
    }
}