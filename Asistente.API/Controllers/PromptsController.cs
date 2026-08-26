using Asistente.Application.Services;
using Asistente.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PromptsController : ControllerBase
{
    private readonly PromptSistemaService _promptService;
    private readonly AsistenteService _asistenteService;
    private readonly Domain.Interfaces.IOllamaService _ollamaService;
    private readonly Microsoft.Extensions.Options.IOptions<OllamaConfig> _ollamaConfig;

    public PromptsController(
        PromptSistemaService promptService,
        AsistenteService asistenteService,
        Domain.Interfaces.IOllamaService ollamaService,
        Microsoft.Extensions.Options.IOptions<OllamaConfig> ollamaConfig)
    {
        _promptService = promptService;
        _asistenteService = asistenteService;
        _ollamaService = ollamaService;
        _ollamaConfig = ollamaConfig;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PromptSistemaDto>>> GetAll()
    {
        var prompts = await _promptService.ObtenerTodosAsync();
        return Ok(prompts);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PromptSistemaDto>> GetById(int id)
    {
        var prompt = await _promptService.ObtenerPorIdAsync(id);
        if (prompt == null) return NotFound("Prompt no encontrado.");
        return Ok(prompt);
    }

    [HttpGet("asistente/{asistenteId}")]
    public async Task<ActionResult<IEnumerable<PromptSistemaDto>>> GetByAsistenteId(int asistenteId)
    {
        var prompts = await _promptService.ObtenerPorAsistenteIdAsync(asistenteId);
        return Ok(prompts);
    }

    [HttpGet("asistente/{asistenteId}/activo")]
    public async Task<ActionResult<PromptSistemaDto>> GetActiveByAsistenteId(int asistenteId)
    {
        var prompt = await _promptService.ObtenerActivoPorAsistenteIdAsync(asistenteId);
        if (prompt == null) return NotFound("No hay prompt activo para este asistente.");
        return Ok(prompt);
    }

    [HttpPost]
    public async Task<ActionResult<PromptSistemaDto>> Create([FromBody] CrearPromptRequest request)
    {
        try
        {
            var prompt = await _promptService.CrearPromptAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = prompt.IdPrompt }, prompt);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<PromptSistemaDto>> Update(int id, [FromBody] ActualizarPromptRequest request)
    {
        try
        {
            var usuario = GetCurrentUser();
            var prompt = await _promptService.ActualizarPromptAsync(id, request, usuario);
            return Ok(prompt);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/activar")]
    public async Task<IActionResult> Activate(int id)
    {
        try
        {
            var usuario = GetCurrentUser();
            await _promptService.ActivarPromptAsync(id, usuario);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPut("{id}/desactivar")]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await _promptService.DesactivarPromptAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _promptService.EliminarPromptAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("{id}/duplicar")]
    public async Task<ActionResult<PromptSistemaDto>> Duplicate(int id)
    {
        try
        {
            var usuario = GetCurrentUser();
            var prompt = await _promptService.DuplicarPromptAsync(id, usuario);
            return CreatedAtAction(nameof(GetById), new { id = prompt.IdPrompt }, prompt);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{id}/historial")]
    public async Task<ActionResult<IEnumerable<HistorialPromptDto>>> GetHistorial(int id)
    {
        var historial = await _promptService.ObtenerHistorialAsync(id);
        return Ok(historial);
    }

    [HttpPost("probar")]
    public async Task<ActionResult<PruebaAsistenteResponse>> Probar([FromBody] PruebaAsistenteRequest request)
    {
        try
        {
            var asistente = await _asistenteService.ObtenerPorIdAsync(request.IdAsistente);
            if (asistente == null)
                return NotFound(new PruebaAsistenteResponse { Exitoso = false, Error = "Asistente no encontrado." });

            PromptSistemaDto? prompt = null;
            if (request.IdPrompt.HasValue)
            {
                prompt = await _promptService.ObtenerPorIdAsync(request.IdPrompt.Value);
            }
            else
            {
                prompt = await _promptService.ObtenerActivoPorAsistenteIdAsync(request.IdAsistente);
            }

            if (prompt == null)
                return NotFound(new PruebaAsistenteResponse { Exitoso = false, Error = "No hay prompt disponible. Cree un prompt primero." });

            var promptGenerado = ConstruirPromptCompleto(asistente, prompt, request.Mensaje);

            var config = _ollamaConfig.Value;
            var modelOverride = asistente.ModeloIA;

            var mensajes = new List<Domain.Entities.Mensaje>();
            mensajes.Add(new Domain.Entities.Mensaje
            {
                Rol = Domain.Enums.RolMensaje.User,
                Contenido = request.Mensaje,
                FechaHora = DateTime.UtcNow
            });

            var inicio = DateTime.UtcNow;
            var respuesta = await _ollamaService.SendMessageAsync(mensajes, modelOverride, prompt.Contenido);
            var tiempoMs = (long)(DateTime.UtcNow - inicio).TotalMilliseconds;

            return Ok(new PruebaAsistenteResponse
            {
                Exitoso = true,
                PromptGenerado = promptGenerado,
                Respuesta = respuesta,
                TiempoRespuestaMs = tiempoMs
            });
        }
        catch (Exception ex)
        {
            return Ok(new PruebaAsistenteResponse
            {
                Exitoso = false,
                Error = ex.Message
            });
        }
    }

    private static string ConstruirPromptCompleto(AsistenteDto asistente, PromptSistemaDto prompt, string mensajeUsuario)
    {
        var partes = new List<string>();

        partes.Add("=== CONFIGURACIÓN DEL ASISTENTE ===");
        partes.Add($"Nombre: {asistente.Nombre}");
        partes.Add($"Modelo IA: {asistente.ModeloIA}");
        partes.Add($"Idioma: {asistente.Idioma ?? "es"}");
        partes.Add($"Nivel de formalidad: {asistente.NivelFormalidad ?? "profesional"}");
        partes.Add($"Formato de respuesta: {asistente.FormatoRespuesta ?? "texto"}");

        if (!string.IsNullOrWhiteSpace(asistente.Restricciones))
        {
            partes.Add("");
            partes.Add("=== RESTRICCIONES ===");
            partes.Add(asistente.Restricciones);
        }

        if (!string.IsNullOrWhiteSpace(asistente.MensajeBienvenida))
        {
            partes.Add("");
            partes.Add("=== MENSAJE DE BIENVENIDA ===");
            partes.Add(asistente.MensajeBienvenida);
        }

        partes.Add("");
        partes.Add("=== PROMPT DEL SISTEMA ===");
        partes.Add(prompt.Contenido);

        partes.Add("");
        partes.Add("=== CONVERSACIÓN ===");
        partes.Add($"Usuario: {mensajeUsuario}");

        if (asistente.LongitudMaximaRespuesta.HasValue)
        {
            partes.Add("");
            partes.Add($"NOTA: La respuesta no debe exceder {asistente.LongitudMaximaRespuesta} caracteres.");
        }

        return string.Join("\n", partes);
    }

    private string GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim != null)
            return userIdClaim;
        return "Sistema";
    }
}
