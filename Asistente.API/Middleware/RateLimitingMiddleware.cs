using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Asistente.API.Middleware;

/// <summary>
/// Rate Limiting de la plataforma (ETAPA 14 - Actividad 13).
/// Limita la cantidad de solicitudes por minuto por usuario/IP y por categoría de operación.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimit;

    public RateLimitingMiddleware(RequestDelegate next, IRateLimitService rateLimit)
    {
        _next = next;
        _rateLimit = rateLimit;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var usuarioId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var llave = !string.IsNullOrEmpty(usuarioId) ? $"u:{usuarioId}" : $"ip:{context.Connection.RemoteIpAddress}";

        // Categoría según la ruta (solicitudes de usuario, ejecución de herramientas, SQL, workflows).
        var ruta = context.Request.Path.Value ?? string.Empty;
        var categoria = ruta switch
        {
            var p when p.Contains("/herramientas", StringComparison.OrdinalIgnoreCase) => "Herramienta",
            var p when p.Contains("/sql", StringComparison.OrdinalIgnoreCase) => "Sql",
            var p when p.Contains("/workflow", StringComparison.OrdinalIgnoreCase) => "Workflow",
            _ => "General"
        };

        // Límite por minuto según categoría (Actividad 13).
        // Ajustado para entorno de piloto/desarrollo: los límites originales (Workflow=10,
        // Sql=10, General=60) se agotaban con el sondeo normal de la Web (GET de listas +
        // dropdowns) impidiendo guardar. Valores elevados para no bloquear operación legítima.
        var limite = categoria switch
        {
            "Herramienta" => 200,
            "Sql" => 100,
            "Workflow" => 200,
            _ => 400
        };

        var clave = $"{llave}:{categoria}";
        var resultado = _rateLimit.RegistrarYVerificar(clave, limite, 60);
        if (!resultado.Permitido)
        {
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = resultado.SegundosBloqueo.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Se ha superado el límite de solicitudes. Intente nuevamente más tarde.",
                categoria,
                segundosBloqueo = resultado.SegundosBloqueo
            });
            return;
        }

        await _next(context);
    }
}
