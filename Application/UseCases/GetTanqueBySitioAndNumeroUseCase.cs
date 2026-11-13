using Application.Interfaces;
using Domain.Entities;

namespace Application.UseCases
{
    public class GetTanqueBySitioAndNumeroUseCase
    {
        private readonly ITanqueRepository _tanqueRepository;

        public GetTanqueBySitioAndNumeroUseCase(ITanqueRepository tanqueRepository)
        {
            _tanqueRepository = tanqueRepository;
        }

        public async Task<TanqueEntity?> ExecuteAsync(int idSitio, int noTanque)
        {
            return await _tanqueRepository.GetTanqueBySitioAndNumeroAsync(idSitio, noTanque);
        }
    }
}