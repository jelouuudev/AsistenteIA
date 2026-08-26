using System.Net;
using System.Text.Json;
using Asistente.Application.DTOs;
using Serilog;

namespace Asistente.API.Middleware;

/// <summary>
/// Middleware global para manejo de excepciones
/// </summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (TimeoutException ex)
        {
            Log.Error(ex, "Timeout detectado: {Message}", ex.Message);
            await HandleExceptionAsync(context, HttpStatusCode.RequestTimeout, "La solicitud excedió el tiempo límite. Intente nuevamente.");
        }
        catch (InvalidOperationException ex)
        {
            Log.Error(ex, "Error de operación: {Message}", ex.Message);
            await HandleExceptionAsync(context, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Error(ex, "Acceso no autorizado: {Message}", ex.Message);
            await HandleExceptionAsync(context, HttpStatusCode.Unauthorized, "No tiene permiso para realizar esta acción.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error no controlado: {Message}", ex.Message);
            await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, "Ocurrió un error inesperado. Intente nuevamente.");
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new ChatResponseDto
        {
            Success = false,
            Error = message
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
