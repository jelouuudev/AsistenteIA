using Asistente.Application.Interfaces;
using Asistente.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost("enviar")]
    public async Task<ActionResult<MensajeResponse>> EnviarMensaje(
        [FromBody] MensajeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Mensaje))
        {
            return BadRequest(new MensajeResponse
            {
                Exitoso = false,
                Error = "El mensaje no puede estar vacío."
            });
        }

        var response = await _chatService.ProcesarMensajeAsync(request, cancellationToken);

        if (!response.Exitoso)
        {
            return StatusCode(500, response);
        }

        return Ok(response);
    }
}
