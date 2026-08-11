using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Asistente.Infrastructure.Data;

namespace Asistente.API.Controllers;

/// <summary>
/// Endpoint de monitoreo y health check para producción (ETAPA 15).
/// </summary>
[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AsistenteDbContext _db;
    private readonly IConfiguration _config;

    public HealthController(AsistenteDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = new Dictionary<string, object>
        {
            ["status"] = "ok",
            ["timestamp"] = DateTime.UtcNow,
            ["checks"] = new Dictionary<string, string>()
        };
        var checks = (Dictionary<string, string>)result["checks"];

        // 1) Base de datos
        try
        {
            await _db.Database.CanConnectAsync();
            checks["database"] = "ok";
        }
        catch (Exception ex)
        {
            checks["database"] = "fail: " + ex.Message;
            result["status"] = "degraded";
        }

        // 2) Ollama
        var ollamaUrl = _config["Ollama:Url"] ?? "http://localhost:11434";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var resp = await client.GetAsync(ollamaUrl.TrimEnd('/') + "/api/tags");
            checks["ollama"] = resp.IsSuccessStatusCode ? "ok" : "fail: " + resp.StatusCode;
            if (!resp.IsSuccessStatusCode) result["status"] = "degraded";
        }
        catch (Exception ex)
        {
            checks["ollama"] = "fail: " + ex.Message;
            result["status"] = "degraded";
        }

        // 3) Chroma (opcional, no bloquea)
        checks["chromadb"] = "not_checked";

        var code = result["status"]!.ToString() == "ok" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
        return StatusCode((int)code, result);
    }
}
