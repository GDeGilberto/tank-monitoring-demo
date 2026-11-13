using Application.Interfaces;
using Domain.Entities;

namespace Application.UseCases
{
    public class GetSitiosUseCase
    {
        private readonly IRepository<SitiosEntity> _repository;
        
        public GetSitiosUseCase(IRepository<SitiosEntity> repository)
            => _repository = repository ?? throw new ArgumentNullException(nameof(repository));

        /// <summary>
        /// Executes the use case to get all stations
        /// </summary>
        /// <returns>A collection of all stations</returns>
        /// <exception cref="Infrastructure.Repositories.DatabaseConnectionException">Thrown when a database connection error occurs</exception>
        /// <exception cref="Infrastructure.Repositories.RepositoryException">Thrown when a repository error occurs</exception>
        public async Task<IEnumerable<SitiosEntity>> ExecuteAsync()
            => await _repository.GetAllAsync();
    }
}
