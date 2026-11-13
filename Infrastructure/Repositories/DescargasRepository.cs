using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Custom repository exceptions for better error handling
    /// </summary>
    public class RepositoryException : Exception 
    {
        public RepositoryException(string message) : base(message) { }
        public RepositoryException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class EntityNotFoundException : RepositoryException 
    {
        public EntityNotFoundException(string message) : base(message) { }
    }

    public class DatabaseConnectionException : RepositoryException 
    {
        public DatabaseConnectionException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class DuplicateEntityException : RepositoryException 
    {
        public DuplicateEntityException(string message) : base(message) { }
    }

    public class DescargasRepository : IRepository<DescargasEntity>, IRepositorySearch<ProcDescargaModel, DescargasEntity>
    {
        private readonly AppDbContext _dbContext;
        
        public DescargasRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task AddAsync(DescargasEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "La entidad de descarga no puede ser nula");
            }

            try
            {
                ProcDescargaModel descarga = new()
                {
                    IdSitio = entity.IdSitio,
                    NoTanque = entity.NoTanque,
                    VolumenInicio = entity.VolumenInicial,
                    TemperaturaInicio = entity.TemperaturaInicial,
                    FechaInicio = entity.FechaInicial,
                    VolumenDisponible = entity.VolumenDisponible,
                    TemperaturaFinal = entity.TemperaturaFinal,
                    FechaFinal = entity.FechaFinal,
                    CantidadCargada = entity.CantidadCargada
                };
                
                // Duplicado: mismo sitio, tanque y fecha-hora de inicio (precision de minuto)
                var isRegistered = await _dbContext.ProcDescargas
                    .FirstOrDefaultAsync(d => d.IdSitio == descarga.IdSitio
                        && d.NoTanque == descarga.NoTanque
                        && d.FechaInicio == descarga.FechaInicio);

                if (isRegistered != null)
                {
                    throw new DuplicateEntityException(
                        $"Ya existe un registro de descarga para el sitio {entity.IdSitio}, " +
                        $"tanque {entity.NoTanque} con inicio {entity.FechaInicial:yyyy-MM-dd HH:mm}");
                }

                await _dbContext.ProcDescargas.AddAsync(descarga);
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException dbEx)
            {
                throw new DatabaseConnectionException(
                    $"Error al guardar la descarga en la base de datos: {dbEx.Message}", dbEx);
            }
            catch (Exception ex) when (!(ex is RepositoryException))
            {
                throw new RepositoryException(
                    $"Error inesperado al agregar la descarga: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<DescargasEntity>> GetAllAsync()
        {
            try
            {
                var result = await _dbContext.ProcDescargas
                    .Select(d => new DescargasEntity
                    (
                        d.Id,
                        d.IdSitio,
                        d.NoTanque,
                        d.VolumenInicio,
                        d.TemperaturaInicio,
                        d.FechaInicio,
                        d.VolumenDisponible,
                        d.TemperaturaFinal,
                        d.FechaFinal,
                        d.CantidadCargada
                    ))
                    .ToListAsync();

                return result;
            }
            catch (DbUpdateException dbEx)
            {
                throw new DatabaseConnectionException(
                    $"Error de base de datos al obtener todas las descargas: {dbEx.Message}", dbEx);
            }
            catch (Exception ex) when (!(ex is RepositoryException))
            {
                throw new RepositoryException(
                    $"Error inesperado al obtener todas las descargas: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<DescargasEntity>> GetAsync(Expression<Func<ProcDescargaModel, bool>> predicate)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate), "El predicado de búsqueda no puede ser nulo");
            }
            
            try
            {
                var descargasModel = await _dbContext.ProcDescargas
                    .Where(predicate)
                    .ToListAsync();

                if (descargasModel == null || !descargasModel.Any())
                {
                    return Enumerable.Empty<DescargasEntity>();
                }

                return descargasModel.Select(d => new DescargasEntity
                (
                    d.Id,
                    d.IdSitio,
                    d.NoTanque,
                    d.VolumenInicio,
                    d.TemperaturaInicio,
                    d.FechaInicio,
                    d.VolumenDisponible,
                    d.TemperaturaFinal,
                    d.FechaFinal,
                    d.CantidadCargada
                ));
            }
            catch (DbUpdateException dbEx)
            {
                throw new DatabaseConnectionException(
                    $"Error de base de datos al buscar descargas: {dbEx.Message}", dbEx);
            }
            catch (Exception ex) when (!(ex is RepositoryException))
            {
                throw new RepositoryException(
                    $"Error inesperado al buscar descargas: {ex.Message}", ex);
            }
        }

        public async Task<DescargasEntity?> GetByIdAsync(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("El ID debe ser un valor positivo", nameof(id));
            }
            
            try
            {
                var descargaModel = await _dbContext.ProcDescargas.FindAsync(id);
                
                if (descargaModel == null)
                {
                    throw new EntityNotFoundException($"No se encontró ninguna descarga con el ID {id}");
                }
                
                return new DescargasEntity
                (
                    descargaModel.Id,
                    descargaModel.IdSitio,
                    descargaModel.NoTanque,
                    descargaModel.VolumenInicio,
                    descargaModel.TemperaturaInicio,
                    descargaModel.FechaInicio,
                    descargaModel.VolumenDisponible,
                    descargaModel.TemperaturaFinal,
                    descargaModel.FechaFinal,
                    descargaModel.CantidadCargada
                );
            }
            catch (DbUpdateException dbEx)
            {
                throw new DatabaseConnectionException(
                    $"Error de base de datos al buscar la descarga con ID {id}: {dbEx.Message}", dbEx);
            }
            catch (Exception ex) when (!(ex is EntityNotFoundException) && !(ex is RepositoryException))
            {
                throw new RepositoryException(
                    $"Error inesperado al buscar la descarga con ID {id}: {ex.Message}", ex);
            }
        }
    }
}
