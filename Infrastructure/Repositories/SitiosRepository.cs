using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class SitiosRepository : IRepository<SitiosEntity>
    {
        private readonly AppDbContext _dbContext;

        public SitiosRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public Task AddAsync(SitiosEntity entity)
        {
            throw new NotImplementedException("La función de agregar sitios no está implementada");
        }

        public async Task<IEnumerable<SitiosEntity>> GetAllAsync()
        {
            try
            {
                var sitios = await _dbContext.CatSitios
                    .Include(e => e.CatTanques)
                    .ToListAsync();

                if (sitios == null || !sitios.Any())
                {
                    // No es un error, simplemente no hay sitios
                    return Enumerable.Empty<SitiosEntity>();
                }

                return sitios.Select(e => MapToEntity(e));
            }
            catch (DbUpdateException dbEx)
            {
                throw new DatabaseConnectionException(
                    $"Error de base de datos al obtener todos los sitios: {dbEx.Message}", dbEx);
            }
            catch (Exception ex) when (!(ex is RepositoryException))
            {
                throw new RepositoryException(
                    $"Error inesperado al obtener todos los sitios: {ex.Message}", ex);
            }
        }

        public async Task<SitiosEntity> GetByIdAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("El ID de sitio debe ser un valor positivo", nameof(id));
            }

            try
            {
                var sitiosModel = await _dbContext.CatSitios
                    .Include(e => e.CatTanques)
                    .FirstOrDefaultAsync(e => e.IdSitio == id);

                if (sitiosModel == null)
                {
                    throw new EntityNotFoundException($"No se encontró ningun sitio con el ID {id}");
                }

                return MapToEntity(sitiosModel);
            }
            catch (DbUpdateException dbEx)
            {
                throw new DatabaseConnectionException(
                    $"Error de base de datos al buscar el sitio con ID {id}: {dbEx.Message}", dbEx);
            }
            catch (Exception ex) when (!(ex is EntityNotFoundException) && !(ex is RepositoryException))
            {
                throw new RepositoryException(
                    $"Error inesperado al buscar el sitio con ID {id}: {ex.Message}", ex);
            }
        }

        private static SitiosEntity MapToEntity(Models.CatSitiosModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model), "El modelo de sitio no puede ser nulo");
            }

            return new SitiosEntity(
                model.IdSitio,
                model.Nombre,
                model.Direccion,
                model.Telefono,
                model.NomContacto,
                model.CorreoElectronico,
                model.Activa,
                model.UltimoEnvio,
                model.EnvioCorreo,
                model.DistUbicacion,
                model.TipoCliente,
                model.Idcliente,
                model.Atiende,
                model.IdZonaPrecio,
                model.UltimaActualizacionAnalisis,
                model.IdSitioRelacionado,
                model.CatTanques.Select(t => new TanquesEntity(
                    t.IdSitio,
                    t.NoTanque,
                    t.Producto,
                    t.Fondeja,
                    t.Capacidad,
                    t.Capacidad95,
                    null
                )).ToList()
            );
        }
    }
}
