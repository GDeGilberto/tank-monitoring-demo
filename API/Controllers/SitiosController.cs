using Application.UseCases;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers
{
    /// <summary>
    /// Controlador para la gestión de sitios de monitoreo
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class SitiosController : ControllerBase
    {
        private readonly GetSitiosUseCase _getSitiosUseCase;
        private readonly GetSitiosByIdUseCase _getSitiosByIdUseCase;
        private readonly ILogger<SitiosController> _logger;

        public SitiosController(
            GetSitiosUseCase getSitiosUseCase,
            GetSitiosByIdUseCase getSitiosByIdUseCase,
            ILogger<SitiosController> logger)
        {
            _getSitiosUseCase = getSitiosUseCase;
            _getSitiosByIdUseCase = getSitiosByIdUseCase;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los sitios de monitoreo
        /// </summary>
        /// <returns>Lista de todos los sitios</returns>
        [HttpGet]
        [SwaggerOperation(
            Summary = "Obtiene todos los sitios",
            Description = "Retorna el listado completo de sitios de monitoreo",
            OperationId = "GetSitios",
            Tags = new[] { "Sitios" }
        )]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<IEnumerable<SitiosEntity>>> Get()
        {
            var result = await _getSitiosUseCase.ExecuteAsync();
            
            if (result == null || !result.Any())
            {
                _logger.LogInformation("No stations found");
                return NoContent();
            }
            
            return Ok(result);
        }

        /// <summary>
        /// Obtiene un sitio por su ID
        /// </summary>
        /// <param name="id">ID del sitio a consultar</param>
        /// <returns>El sitio solicitada</returns>
        [HttpGet("{id:int}")]
        [SwaggerOperation(
            Summary = "Obtiene un sitio por su ID",
            Description = "Retorna información detallada de un sitio específica",
            OperationId = "GetSitioById",
            Tags = new[] { "Sitios" }
        )]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<SitiosEntity>> GetById(int id)
        {
            if (id <= 0)
            {
                _logger.LogWarning("Invalid station ID provided: {Id}", id);
                return BadRequest(new { message = "El ID de sitio proporcionado no es válido" });
            }

            var result = await _getSitiosByIdUseCase.ExecuteAsync(id);
            
            if (result == null)
            {
                _logger.LogInformation("Station with ID {Id} not found", id);
                return NotFound(new { message = $"No se encontró un sitio con el ID {id}" });
            }
            
            _logger.LogInformation("Retrieved station with ID {Id}", id);
            return Ok(result);
        }
    }
}