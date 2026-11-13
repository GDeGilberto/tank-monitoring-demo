using Domain.Entities;
using Application.UseCases;
using Infrastructure.Dtos;
using Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers
{
    /// <summary>
    /// Controlador para la gestión de inventarios de tanques
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class InventariosController : ControllerBase
    {
        private readonly GetLatestInventarioByStationUseCase<ProcInventarioModel> _getLatestInventarioByStationUseCase;
        private readonly GetInventarioByIdUseCase _getInventarioByIdUseCase;
        private readonly CreateInventarioUseCase<InventarioRequestDTO> _createInventarioUseCase;
        private readonly ILogger<InventariosController> _logger;

        public InventariosController(
            GetLatestInventarioByStationUseCase<ProcInventarioModel> getLatestInventarioByStationUseCase,
            GetInventarioByIdUseCase getInventarioByIdUseCase,
            CreateInventarioUseCase<InventarioRequestDTO> createInventarioUseCase,
            ILogger<InventariosController> logger)
        {
            _getLatestInventarioByStationUseCase = getLatestInventarioByStationUseCase;
            _getInventarioByIdUseCase = getInventarioByIdUseCase;
            _createInventarioUseCase = createInventarioUseCase;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene el inventario más reciente por ID de sitio
        /// </summary>
        /// <param name="idSitio">ID del sitio</param>
        /// <returns>Los registros de inventario más recientes para el sitio especificada</returns>
        [HttpGet("{idSitio:int}")]
        [SwaggerOperation(
            Summary = "Obtiene el inventario más reciente por sitio",
            Description = "Retorna los registros de inventario más recientes para un sitio específica",
            OperationId = "GetInventarioBySitio",
            Tags = new[] { "Inventarios" }
        )]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<IEnumerable<InventarioEntity>>> GetBySitio(int idSitio)
        {
            if (idSitio <= 0)
            {
                _logger.LogWarning("Invalid station ID provided: {IdSitio}", idSitio);
                return BadRequest(new { message = "El ID de sitio proporcionado no es válido" });
            }

            var result = await _getLatestInventarioByStationUseCase.ExecuteAsync(s => s.Idsitio == idSitio);
            
            if (result == null || !result.Any())
            {
                _logger.LogInformation("No inventory records found for station {IdSitio}", idSitio);
                return NoContent();
            }
            
            return Ok(result);
        }

        /// <summary>
        /// Obtiene un registro de inventario por su ID
        /// </summary>
        /// <param name="id">ID del registro de inventario a consultar</param>
        /// <returns>El registro de inventario solicitado</returns>
        [HttpGet("id/{id:int}")]
        [SwaggerOperation(
            Summary = "Obtiene un inventario por su ID",
            Description = "Retorna información detallada de un registro de inventario específico",
            OperationId = "GetInventarioById",
            Tags = new[] { "Inventarios" }
        )]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<InventarioEntity>> GetById(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid inventory ID provided: {Id}", id);
                return BadRequest(new { message = "El ID proporcionado no es válido" });
            }

            var result = await _getInventarioByIdUseCase.ExecuteAsync(id);
            
            if (result == null)
            {
                _logger.LogInformation("Inventory with ID {Id} not found", id);
                return NotFound(new { message = $"No se encontró un inventario con el ID {id}" });
            }
            
            return Ok(result);
        }

        /// <summary>
        /// Crea un nuevo registro de inventario
        /// </summary>
        /// <param name="inventarioRequest">Datos del inventario a crear</param>
        /// <returns>Respuesta indicando el éxito o fracaso de la operación</returns>
        [HttpPost]
        [SwaggerOperation(
            Summary = "Crea un nuevo registro de inventario",
            Description = "Registra un nuevo inventario en el sistema",
            OperationId = "CreateInventario",
            Tags = new[] { "Inventarios" }
        )]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult> Create([FromBody] InventarioRequestDTO inventarioRequest)
        {
            if (inventarioRequest == null)
            {
                return BadRequest(new { message = "El cuerpo de la solicitud no puede estar vacío" });
            }

            var validationErrors = ValidateInventarioRequest(inventarioRequest);
            if (validationErrors.Any())
            {
                return BadRequest(new { message = "Datos de inventario inválidos", errors = validationErrors });
            }

            await _createInventarioUseCase.ExecuteAsync(inventarioRequest);
            
            _logger.LogInformation(
                "Inventory created successfully for Sitio {IdSitio} and Tanque {NoTanque}",
                inventarioRequest.IdSitio, inventarioRequest.NoTanque);
            
            return Created($"/api/inventarios", new { 
                message = "Inventario creado con éxito", 
                idSitio = inventarioRequest.IdSitio,
                noTanque = inventarioRequest.NoTanque
            });
        }

        private static List<string> ValidateInventarioRequest(InventarioRequestDTO request)
        {
            var errors = new List<string>();
            
            if (request.IdSitio <= 0)
                errors.Add("El ID de sitio es obligatorio y debe ser positivo");
                
            if (request.NoTanque <= 0)
                errors.Add("El número de tanque es obligatorio y debe ser positivo");
                
            if (request.VolumenDisponible <= 0)
                errors.Add("El volumen disponible debe ser un valor positivo");
                
            return errors;
        }
    }
}