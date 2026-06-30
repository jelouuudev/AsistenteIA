using Asistente.Shared;
using Asistente.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Web.Controllers;

public class ChatController : Controller
{
    private readonly IApiService _apiService;

    public ChatController(IApiService apiService)
    {
        _apiService = apiService;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<ActionResult<MensajeResponse>> Enviar([FromBody] MensajeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Mensaje))
        {
            return BadRequest(new MensajeResponse
            {
                Exitoso = false,
                Error = "El mensaje no puede estar vacío."
            });
        }

        var response = await _apiService.EnviarMensajeAsync(request);
        return Ok(response);
    }
}
