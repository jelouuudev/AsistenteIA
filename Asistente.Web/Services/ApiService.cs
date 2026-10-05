using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Asistente.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Asistente.Web.Services;

public class ApiService : IApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions _jsonSendOptions;

    public ApiService(HttpClient httpClient, ILogger<ApiService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _jsonSendOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private HttpRequestMessage CrearRequest(HttpMethod method, string uri, object? content, int currentUserId, string ip)
    {
        var request = new HttpRequestMessage(method, uri);
        if (content != null)
        {
            var json = JsonSerializer.Serialize(content);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        // Token JWT del usuario autenticado (claim JwtToken cargado en el login del Web).
        // Sin esto el API devuelve 401 y ninguna operación de la Web se guarda.
        var token = _httpContextAccessor.HttpContext?.User?.FindFirst("JwtToken")?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-User-Id", currentUserId.ToString());
        request.Headers.Add("X-User-IP", ip);
        return request;
    }

    private async Task<T> EnviarYLeerAsync<T>(HttpRequestMessage request)
    {
        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(string.IsNullOrEmpty(content) ? $"Error del servidor: {response.StatusCode}" : content);
        }

        return JsonSerializer.Deserialize<T>(content, _jsonOptions)
            ?? throw new Exception("Error al deserializar la respuesta.");
    }

    private async Task<T?> EnviarYLeerOpcionalAsync<T>(HttpRequestMessage request) where T : class
    {
        try
        {
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private async Task EnviarSinRetornoAsync(HttpRequestMessage request)
    {
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception(string.IsNullOrEmpty(content) ? $"Error del servidor: {response.StatusCode}" : content);
        }
    }

    public async Task<MensajeResponse> EnviarMensajeAsync(MensajeRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonSendOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/chat/enviar", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<MensajeResponse>(responseContent, _jsonOptions);

            return result ?? new MensajeResponse
            {
                Exitoso = false,
                Error = "Error al procesar la respuesta del servidor."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Error de conexión con la API.");
            return new MensajeResponse
            {
                Exitoso = false,
                Error = "No se puede conectar con el servicio de IA. Verifique que la API esté ejecutándose."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al llamar a la API.");
            return new MensajeResponse
            {
                Exitoso = false,
                Error = ex.Message
            };
        }
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login");
            var json = JsonSerializer.Serialize(request);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Add("X-User-IP", request.DireccionIP ?? "127.0.0.1");

            var response = await _httpClient.SendAsync(req);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var errorResult = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);
                    if (errorResult != null) return errorResult;
                }
                catch { }

                return new LoginResponse { Exitoso = false, Error = string.IsNullOrEmpty(content) ? "Credenciales incorrectas." : content };
            }

            return JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions)
                ?? new LoginResponse { Exitoso = false, Error = "Error de deserialización." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al iniciar sesión.");
            return new LoginResponse { Exitoso = false, Error = "No se pudo conectar con el servidor." };
        }
    }

    public async Task LogoutAsync(int sessionId)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/auth/logout/{sessionId}");
        await _httpClient.SendAsync(req);
    }

    public async Task<IEnumerable<UsuarioDto>> GetUsuariosAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/usuarios", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<UsuarioDto>>(req);
    }

    public async Task<UsuarioDto?> GetUsuarioByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/usuarios/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<UsuarioDto>(req);
        }
        catch
        {
            return null;
        }
    }

    public async Task<UsuarioDto> CrearUsuarioAsync(CrearUsuarioRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/usuarios", request, currentUserId, ip);
        return await EnviarYLeerAsync<UsuarioDto>(req);
    }

    public async Task<UsuarioDto> ActualizarUsuarioAsync(int id, ActualizarUsuarioRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/usuarios/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<UsuarioDto>(req);
    }

    public async Task DesactivarUsuarioAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/usuarios/{id}/desactivar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task CambiarPasswordAsync(int id, CambiarPasswordRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/usuarios/{id}/cambiar-contrasena", request, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task<IEnumerable<RolDto>> GetRolesAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/roles", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<RolDto>>(req);
    }

    public async Task<RolDto?> GetRolByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/roles/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<RolDto>(req);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<FuenteConocimientoDto>> GetFuentesConocimientoAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/documentos/fuentes-disponibles", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<FuenteConocimientoDto>>(req);
    }

    public async Task<IEnumerable<FuenteConocimientoDto>> GetFuentesDocumentoAsync(int idDocumento, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/documentos/{idDocumento}/fuentes", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<FuenteConocimientoDto>>(req);
    }

    public async Task AsignarFuentesDocumentoAsync(int idDocumento, List<int> fuentes, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/documentos/{idDocumento}/fuentes", new { Fuentes = fuentes }, currentUserId, ip);
        await EnviarYLeerAsync<object>(req);
    }

    public async Task<RolDto> CrearRolAsync(CrearRolRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/roles", request, currentUserId, ip);
        return await EnviarYLeerAsync<RolDto>(req);
    }

    public async Task<RolDto> ActualizarRolAsync(int id, ActualizarRolRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/roles/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<RolDto>(req);
    }

    public async Task<IEnumerable<AuditoriaSesionDto>> GetAuditoriaSesionesAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/auditoria/sesiones", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AuditoriaSesionDto>>(req);
    }

    public async Task<IEnumerable<AuditoriaActividadDto>> GetAuditoriaActividadesAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/auditoria/actividades", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AuditoriaActividadDto>>(req);
    }

    // Asistentes
    public async Task<IEnumerable<AsistenteDto>> GetAsistentesAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/asistentes", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AsistenteDto>>(req);
    }

    public async Task<IEnumerable<AsistenteDto>> GetAgentesAutorizadosAsync(int idUsuario, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/agentes/autorizados/{idUsuario}", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AsistenteDto>>(req);
    }

    public async Task<AsistenteDto?> GetAsistenteByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/asistentes/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<AsistenteDto>(req);
        }
        catch { return null; }
    }

    public async Task<AsistenteDto> CrearAsistenteAsync(CrearAsistenteRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/asistentes", request, currentUserId, ip);
        return await EnviarYLeerAsync<AsistenteDto>(req);
    }

    public async Task<AsistenteDto> ActualizarAsistenteAsync(int id, ActualizarAsistenteRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/asistentes/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<AsistenteDto>(req);
    }

    public async Task ActivarAsistenteAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/asistentes/{id}/activar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task DesactivarAsistenteAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/asistentes/{id}/desactivar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    // === ETAPA 16 - Plataforma Multiagente ===
    public async Task PublicarAgenteAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/agentes/{id}/publicar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task EnviarAgenteAPruebaAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/agentes/{id}/enviar-prueba", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task<AsistenteDto> DuplicarAgenteAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/agentes/{id}/duplicar", null, currentUserId, ip);
        return await EnviarYLeerAsync<AsistenteDto>(req);
    }

    public async Task<AgenteVersionDto> CrearVersionAgenteAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/agentes/{id}/versiones", null, currentUserId, ip);
        return await EnviarYLeerAsync<AgenteVersionDto>(req);
    }

    public async Task<AgenteVersionDto> RestaurarVersionAgenteAsync(int id, int idVersion, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/agentes/{id}/versiones/{idVersion}/restaurar", null, currentUserId, ip);
        return await EnviarYLeerAsync<AgenteVersionDto>(req);
    }

    public async Task<IEnumerable<AgenteVersionDto>> GetVersionesAgenteAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/agentes/{id}/versiones", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AgenteVersionDto>>(req);
    }

    public async Task<IEnumerable<PromptSistemaDto>> GetPromptsByAsistenteIdAsync(int asistenteId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/prompts/asistente/{asistenteId}", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<PromptSistemaDto>>(req);
    }

    public async Task<PromptSistemaDto?> GetPromptActivoByAsistenteIdAsync(int asistenteId, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/prompts/asistente/{asistenteId}/activo", null, currentUserId, ip);
            return await EnviarYLeerAsync<PromptSistemaDto>(req);
        }
        catch { return null; }
    }

    // Prompts
    public async Task<IEnumerable<PromptSistemaDto>> GetPromptsAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/prompts", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<PromptSistemaDto>>(req);
    }

    public async Task<PromptSistemaDto?> GetPromptByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/prompts/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<PromptSistemaDto>(req);
        }
        catch { return null; }
    }

    public async Task<PromptSistemaDto> CrearPromptAsync(CrearPromptRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/prompts", request, currentUserId, ip);
        return await EnviarYLeerAsync<PromptSistemaDto>(req);
    }

    public async Task<PromptSistemaDto> ActualizarPromptAsync(int id, ActualizarPromptRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/prompts/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<PromptSistemaDto>(req);
    }

    public async Task ActivarPromptAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/prompts/{id}/activar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task DesactivarPromptAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/prompts/{id}/desactivar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task EliminarPromptAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/prompts/{id}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task<PromptSistemaDto> DuplicarPromptAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/prompts/{id}/duplicar", null, currentUserId, ip);
        return await EnviarYLeerAsync<PromptSistemaDto>(req);
    }

    public async Task<PromptSistemaDto> RestaurarPromptDesdeHistorialAsync(int id, int idHistorial, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/prompts/{id}/historial/{idHistorial}/restaurar", null, currentUserId, ip);
        return await EnviarYLeerAsync<PromptSistemaDto>(req);
    }

    public async Task<IEnumerable<HistorialPromptDto>> GetHistorialPromptAsync(int promptId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/prompts/{promptId}/historial", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<HistorialPromptDto>>(req);
    }

    public async Task<PruebaAsistenteResponse> ProbarAsistenteAsync(PruebaAsistenteRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/prompts/probar", request, currentUserId, ip);
        return await EnviarYLeerAsync<PruebaAsistenteResponse>(req);
    }

    // Memoria - Conversaciones
    public async Task<IEnumerable<ConversacionListDto>> GetConversacionesAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/memoria/conversaciones", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<ConversacionListDto>>(req);
    }

    public async Task<IEnumerable<ConversacionListDto>> BuscarConversacionesAsync(string query, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/memoria/conversaciones/buscar?q={Uri.EscapeDataString(query)}", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<ConversacionListDto>>(req);
    }

    public async Task<ConversacionDto?> GetConversacionByIdAsync(int id, int currentUserId, string ip)
    {
        return await EnviarYLeerOpcionalAsync<ConversacionDto>(
            CrearRequest(HttpMethod.Get, $"/api/memoria/conversaciones/{id}", null, currentUserId, ip));
    }

    public async Task<ConversacionDto> CrearConversacionAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/memoria/conversaciones", null, currentUserId, ip);
        return await EnviarYLeerAsync<ConversacionDto>(req);
    }

    public async Task RenombrarConversacionAsync(int id, string titulo, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/memoria/conversaciones/{id}/renombrar",
            new RenombrarConversacionRequest { Titulo = titulo }, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task ArchivarConversacionAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/memoria/conversaciones/{id}/archivar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task EliminarConversacionAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/memoria/conversaciones/{id}/eliminar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task<DebugContextoDto?> GetDebugContextoAsync(int idConversacion, int currentUserId, string ip)
    {
        return await EnviarYLeerOpcionalAsync<DebugContextoDto>(
            CrearRequest(HttpMethod.Get, $"/api/memoria/conversaciones/{idConversacion}/debug", null, currentUserId, ip));
    }

    // Configuracion Memoria
    public async Task<ConfiguracionMemoriaDto> GetConfiguracionMemoriaAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/configuracionmemoria", null, currentUserId, ip);
        return await EnviarYLeerAsync<ConfiguracionMemoriaDto>(req);
    }

    public async Task<ConfiguracionMemoriaDto> ActualizarConfiguracionMemoriaAsync(ActualizarConfiguracionMemoriaRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, "/api/configuracionmemoria", request, currentUserId, ip);
        return await EnviarYLeerAsync<ConfiguracionMemoriaDto>(req);
    }

    // Categorias Documento
    public async Task<IEnumerable<CategoriaDocumentoDto>> GetCategoriasDocumentoAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/categoriasdocumento", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<CategoriaDocumentoDto>>(req);
    }

    public async Task<IEnumerable<CategoriaDocumentoDto>> GetCategoriasDocumentoActivasAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/categoriasdocumento/activas", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<CategoriaDocumentoDto>>(req);
    }

    public async Task<CategoriaDocumentoDto?> GetCategoriaDocumentoByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/categoriasdocumento/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<CategoriaDocumentoDto>(req);
        }
        catch
        {
            return null;
        }
    }

    public async Task<CategoriaDocumentoDto> CrearCategoriaDocumentoAsync(CrearCategoriaDocumentoRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/categoriasdocumento", request, currentUserId, ip);
        return await EnviarYLeerAsync<CategoriaDocumentoDto>(req);
    }

    public async Task<CategoriaDocumentoDto> ActualizarCategoriaDocumentoAsync(int id, ActualizarCategoriaDocumentoRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/categoriasdocumento/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<CategoriaDocumentoDto>(req);
    }

    // Documentos
    public async Task<IEnumerable<DocumentoDto>> GetDocumentosFiltradosAsync(FiltroDocumentoRequest request, int currentUserId, string ip)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Nombre))
            queryParams.Add($"nombre={Uri.EscapeDataString(request.Nombre)}");
        if (request.IdCategoria.HasValue)
            queryParams.Add($"idCategoria={request.IdCategoria.Value}");
        if (!string.IsNullOrWhiteSpace(request.Estado))
            queryParams.Add($"estado={Uri.EscapeDataString(request.Estado)}");
        if (request.FechaDesde.HasValue)
            queryParams.Add($"fechaDesde={request.FechaDesde.Value:yyyy-MM-dd}");
        if (request.FechaHasta.HasValue)
            queryParams.Add($"fechaHasta={request.FechaHasta.Value:yyyy-MM-dd}");

        var query = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
        var req = CrearRequest(HttpMethod.Get, $"/api/documentos{query}", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoDto>>(req);
    }

    public async Task<DocumentoDto?> GetDocumentoByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/documentos/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<DocumentoDto>(req);
        }
        catch
        {
            return null;
        }
    }

    public async Task<DocumentoDto> CrearDocumentoAsync(CrearDocumentoRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/documentos", request, currentUserId, ip);
        return await EnviarYLeerAsync<DocumentoDto>(req);
    }

    public async Task<DocumentoDto> ActualizarDocumentoAsync(int id, ActualizarDocumentoRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/documentos/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<DocumentoDto>(req);
    }

    public async Task<IEnumerable<DocumentoDto>> GetDocumentosDisponiblesAsync(int? idFuenteExcluir, int currentUserId, string ip)
    {
        var url = "/api/documentos/disponibles";
        if (idFuenteExcluir.HasValue)
            url += $"?idFuenteExcluir={idFuenteExcluir.Value}";

        var req = CrearRequest(HttpMethod.Get, url, null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoDto>>(req);
    }

    public async Task ActivarDocumentoAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/documentos/{id}/activar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task ArchivarDocumentoAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/documentos/{id}/archivar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task DiligenciarDocumentoAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/documentos/{id}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task EliminarDocumentoAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/documentos/{id}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    // Versiones
    public async Task<IEnumerable<DocumentoVersionDto>> GetDocumentoVersionesAsync(int documentoId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/documentos/{documentoId}/versiones", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoVersionDto>>(req);
    }

    public async Task<DocumentoVersionDto> CargarDocumentoVersionAsync(int documentoId, string nombreArchivo, Stream archivoStream, int currentUserId, string ip)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/documentos/{documentoId}/versiones");
        req.Headers.Add("X-User-Id", currentUserId.ToString());
        req.Headers.Add("X-User-IP", ip);

        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(archivoStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", nombreArchivo);
        req.Content = content;

        return await EnviarYLeerAsync<DocumentoVersionDto>(req);
    }

    public async Task<(Stream fileStream, string fileName, string contentType)> DescargarDocumentoVersionAsync(int documentoId, int versionId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/documentos/{documentoId}/versiones/{versionId}/descargar", null, currentUserId, ip);
        var response = await _httpClient.SendAsync(req);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Exception(string.IsNullOrEmpty(content) ? $"Error del servidor: {response.StatusCode}" : content);
        }

        var stream = await response.Content.ReadAsStreamAsync();
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar 
            ?? response.Content.Headers.ContentDisposition?.FileName 
            ?? "documento.pdf";
        
        fileName = fileName.Trim('"');
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";

        return (stream, fileName, contentType);
    }

    // Auditoría
    public async Task<IEnumerable<AuditoriaDocumentalDto>> GetAuditoriaDocumentoAsync(int documentoId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/documentos/{documentoId}/auditoria", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AuditoriaDocumentalDto>>(req);
    }

    public async Task<IEnumerable<AuditoriaDocumentalDto>> GetTodasAuditoriasDocumentoAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/documentos/auditoria/todas", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AuditoriaDocumentalDto>>(req);
    }

    // Procesamiento Documental
    public async Task<IEnumerable<DocumentoProcesadoDto>> GetProcesamientoAllAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/procesamiento", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoProcesadoDto>>(req);
    }

    public async Task<DashboardProcesamientoDto> GetProcesamientoDashboardAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/procesamiento/dashboard", null, currentUserId, ip);
        return await EnviarYLeerAsync<DashboardProcesamientoDto>(req);
    }

    public async Task<DocumentoProcesadoDto?> GetProcesamientoByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/procesamiento/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<DocumentoProcesadoDto>(req);
        }
        catch { return null; }
    }

    public async Task<IEnumerable<DocumentoProcesadoDto>> GetProcesamientoByEstadoAsync(string estado, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/procesamiento/estado/{estado}", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoProcesadoDto>>(req);
    }

    public async Task<DocumentoProcesadoDto?> GetProcesamientoByVersionIdAsync(int versionId, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/procesamiento/version/{versionId}", null, currentUserId, ip);
            return await EnviarYLeerAsync<DocumentoProcesadoDto>(req);
        }
        catch { return null; }
    }

    public async Task<IEnumerable<DocumentoChunkDto>> GetChunksAsync(int procesadoId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/procesamiento/{procesadoId}/chunks", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoChunkDto>>(req);
    }

    public async Task ProcesarDocumentoAsync(int versionId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/procesamiento/procesar/{versionId}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task ReprocesarDocumentoAsync(int procesadoId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/procesamiento/reprocesar/{procesadoId}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    // Indexacion
    public async Task<IEnumerable<DocumentoIndexadoDto>> GetIndexacionAllAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/indexacion", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoIndexadoDto>>(req);
    }

    public async Task<DashboardIndexacionDto> GetIndexacionDashboardAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/indexacion/dashboard", null, currentUserId, ip);
        return await EnviarYLeerAsync<DashboardIndexacionDto>(req);
    }

    public async Task<DocumentoIndexadoDto?> GetIndexacionByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/indexacion/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<DocumentoIndexadoDto>(req);
        }
        catch { return null; }
    }

    public async Task<DocumentoIndexadoDto?> GetIndexacionByProcesadoIdAsync(int procesadoId, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/indexacion/procesado/{procesadoId}", null, currentUserId, ip);
            return await EnviarYLeerAsync<DocumentoIndexadoDto>(req);
        }
        catch { return null; }
    }

    public async Task<IEnumerable<DocumentoIndexadoDto>> GetIndexacionByEstadoAsync(string estado, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/indexacion/estado/{estado}", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoIndexadoDto>>(req);
    }

    public async Task IndexarDocumentoAsync(int documentoProcesadoId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/indexacion/indexar/{documentoProcesadoId}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task ReindexarDocumentoAsync(int documentoProcesadoId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/indexacion/reindexar/{documentoProcesadoId}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task ReindexarTodosAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/indexacion/reindexar-todos", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task EliminarIndiceAsync(int documentoProcesadoId, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/indexacion/eliminar/{documentoProcesadoId}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    // Embedding Configuracion
    public async Task<IEnumerable<EmbeddingConfiguracionDto>> GetEmbeddingConfiguracionesAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/embeddingconfiguracion", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<EmbeddingConfiguracionDto>>(req);
    }

    public async Task<EmbeddingConfiguracionDto?> GetEmbeddingConfiguracionActivaAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/embeddingconfiguracion/activa", null, currentUserId, ip);
            return await EnviarYLeerAsync<EmbeddingConfiguracionDto>(req);
        }
        catch { return null; }
    }

    public async Task<EmbeddingConfiguracionDto> ActualizarEmbeddingConfiguracionAsync(int id, ActualizarEmbeddingConfiguracionRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/embeddingconfiguracion/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<EmbeddingConfiguracionDto>(req);
    }

    public async Task<EmbeddingConfiguracionDto> CrearEmbeddingConfiguracionAsync(ActualizarEmbeddingConfiguracionRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/embeddingconfiguracion", request, currentUserId, ip);
        return await EnviarYLeerAsync<EmbeddingConfiguracionDto>(req);
    }

    // Busqueda Semantica
    public async Task<BusquedaSemanticaResponse> BuscarSemanticamenteAsync(BusquedaSemanticaRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/monitoreo/buscar", request, currentUserId, ip);
        return await EnviarYLeerAsync<BusquedaSemanticaResponse>(req);
    }



    public async Task<IEnumerable<FuenteConocimientoDto>> GetFuentesConocimientoActivasAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/fuentesconocimiento/activas", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<FuenteConocimientoDto>>(req);
    }

    public async Task<FuenteConocimientoDto?> GetFuenteConocimientoByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/fuentesconocimiento/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<FuenteConocimientoDto>(req);
        }
        catch { return null; }
    }

    public async Task<FuenteConocimientoDto> CrearFuenteConocimientoAsync(CrearFuenteConocimientoRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/fuentesconocimiento", request, currentUserId, ip);
        return await EnviarYLeerAsync<FuenteConocimientoDto>(req);
    }

    public async Task<FuenteConocimientoDto> ActualizarFuenteConocimientoAsync(int id, ActualizarFuenteConocimientoRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/fuentesconocimiento/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<FuenteConocimientoDto>(req);
    }

    public async Task ActivarFuenteConocimientoAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/fuentesconocimiento/{id}/activar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task DesactivarFuenteConocimientoAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/fuentesconocimiento/{id}/desactivar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task<DashboardFuentesDto> GetDashboardFuentesAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/fuentesconocimiento/dashboard", null, currentUserId, ip);
        return await EnviarYLeerAsync<DashboardFuentesDto>(req);
    }

    public async Task<IEnumerable<AsistenteFuenteDto>> GetFuentesDeAsistenteAsync(int idAsistente, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/fuentesconocimiento/asistente/{idAsistente}/fuentes", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AsistenteFuenteDto>>(req);
    }

    public async Task<IEnumerable<AsistenteFuenteDto>> GetAsistentesDeFuenteAsync(int idFuente, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/fuentesconocimiento/fuente/{idFuente}/asistentes", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<AsistenteFuenteDto>>(req);
    }

    public async Task AsignarFuenteAAsistenteAsync(AsignarFuenteAAsistenteRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/fuentesconocimiento/asistente/asignar", request, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task DesasignarFuenteDeAsistenteAsync(int idAsistente, int idFuente, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/fuentesconocimiento/asistente/{idAsistente}/fuente/{idFuente}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task<IEnumerable<DocumentoFuenteDto>> GetDocumentosDeFuenteAsync(int idFuente, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/fuentesconocimiento/fuente/{idFuente}/documentos", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<DocumentoFuenteDto>>(req);
    }

    public async Task AsignarDocumentoAFuenteAsync(AsignarDocumentoAFuenteRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/fuentesconocimiento/documento/asignar", request, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task DesasignarDocumentoDeFuenteAsync(int idDocumento, int idFuente, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/fuentesconocimiento/documento/{idDocumento}/fuente/{idFuente}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task<ConfiguracionRAGDto> GetConfiguracionRAGAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/configuracionrag", null, currentUserId, ip);
        return await EnviarYLeerAsync<ConfiguracionRAGDto>(req);
    }

    public async Task<ConfiguracionRAGDto> ActualizarConfiguracionRAGAsync(ActualizarConfiguracionRAGRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, "/api/configuracionrag", request, currentUserId, ip);
        return await EnviarYLeerAsync<ConfiguracionRAGDto>(req);
    }

    // Motor de Consultas Empresariales
    public async Task<IEnumerable<ConexionBaseDatosDto>> GetConexionesBaseDatosAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/conexionesbasedatos", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<ConexionBaseDatosDto>>(req);
    }

    public async Task<IEnumerable<ConexionBaseDatosDto>> GetConexionesBaseDatosActivasAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/conexionesbasedatos/activas", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<ConexionBaseDatosDto>>(req);
    }

    public async Task<ConexionBaseDatosDto?> GetConexionBaseDatosByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/conexionesbasedatos/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<ConexionBaseDatosDto>(req);
        }
        catch { return null; }
    }

    public async Task<ConexionBaseDatosDto> CrearConexionBaseDatosAsync(CrearConexionBaseDatosRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/conexionesbasedatos", request, currentUserId, ip);
        return await EnviarYLeerAsync<ConexionBaseDatosDto>(req);
    }

    public async Task<ConexionBaseDatosDto> ActualizarConexionBaseDatosAsync(int id, ActualizarConexionBaseDatosRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/conexionesbasedatos/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<ConexionBaseDatosDto>(req);
    }

    public async Task EliminarConexionBaseDatosAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/conexionesbasedatos/{id}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task ActivarConexionBaseDatosAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/conexionesbasedatos/{id}/activar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task DesactivarConexionBaseDatosAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/conexionesbasedatos/{id}/desactivar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task<ConexionPruebaResultadoDto> ProbarConexionBaseDatosAsync(ProbarConexionRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/conexionesbasedatos/probar", request, currentUserId, ip);
        return await EnviarYLeerAsync<ConexionPruebaResultadoDto>(req);
    }

    public async Task<EsquemaBaseDatosDto> DescubrirEsquemaAsync(int idConexion, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/conexionesbasedatos/{idConexion}/esquema", null, currentUserId, ip);
        return await EnviarYLeerAsync<EsquemaBaseDatosDto>(req);
    }

    public async Task<IEnumerable<TablaAutorizadaDto>> GetTablasAutorizadasAsync(int idConexion, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/conexionesbasedatos/{idConexion}/tablas-autorizadas", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<TablaAutorizadaDto>>(req);
    }

    public async Task<IEnumerable<VistaAutorizadaDto>> GetVistasAutorizadasAsync(int idConexion, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/conexionesbasedatos/{idConexion}/vistas-autorizadas", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<VistaAutorizadaDto>>(req);
    }

    public async Task AgregarTablaAutorizadaAsync(int idConexion, TablaAutorizadaRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/conexionesbasedatos/{idConexion}/tablas-autorizadas", request, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task AgregarVistaAutorizadaAsync(int idConexion, VistaAutorizadaRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, $"/api/conexionesbasedatos/{idConexion}/vistas-autorizadas", request, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task EliminarTablaAutorizadaAsync(int idConexion, int idTabla, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/conexionesbasedatos/{idConexion}/tablas-autorizadas/{idTabla}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task EliminarVistaAutorizadaAsync(int idConexion, int idVista, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/conexionesbasedatos/{idConexion}/vistas-autorizadas/{idVista}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    // Consultas Ejecutadas
    public async Task<IEnumerable<ConsultaEjecutadaDto>> GetConsultasEjecutadasAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/consultasejecutadas", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<ConsultaEjecutadaDto>>(req);
    }

    public async Task<IEnumerable<ConsultaEjecutadaDto>> GetConsultasEjecutadasPorUsuarioAsync(int idUsuario, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, $"/api/consultasejecutadas/usuario/{idUsuario}", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<ConsultaEjecutadaDto>>(req);
    }

    public async Task<DashboardConsultasDto> GetDashboardConsultasAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/consultasejecutadas/dashboard", null, currentUserId, ip);
        return await EnviarYLeerAsync<DashboardConsultasDto>(req);
    }

    public async Task<ResultadoProcesarPreguntaDto> ProcesarPreguntaEmpresarialAsync(ProcesarPreguntaRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/consultasempresariales/procesar", request, currentUserId, ip);
        return await EnviarYLeerAsync<ResultadoProcesarPreguntaDto>(req);
    }

    public async Task<EjecutarConsultaResponse> EjecutarConsultaEmpresarialAsync(EjecutarConsultaRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/consultasempresariales/ejecutar", request, currentUserId, ip);
        return await EnviarYLeerAsync<EjecutarConsultaResponse>(req);
    }

    // Plantillas
    public async Task<IEnumerable<ConsultaPlantillaDto>> GetConsultasPlantillasAsync(int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Get, "/api/consultasplantillas", null, currentUserId, ip);
        return await EnviarYLeerAsync<IEnumerable<ConsultaPlantillaDto>>(req);
    }

    public async Task<ConsultaPlantillaDto?> GetConsultaPlantillaByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/consultasplantillas/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<ConsultaPlantillaDto>(req);
        }
        catch { return null; }
    }

    public async Task<ConsultaPlantillaDto> CrearConsultaPlantillaAsync(CrearConsultaPlantillaRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/consultasplantillas", request, currentUserId, ip);
        return await EnviarYLeerAsync<ConsultaPlantillaDto>(req);
    }

    public async Task<ConsultaPlantillaDto> ActualizarConsultaPlantillaAsync(int id, ActualizarConsultaPlantillaRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/consultasplantillas/{id}", request, currentUserId, ip);
        return await EnviarYLeerAsync<ConsultaPlantillaDto>(req);
    }

    public async Task EliminarConsultaPlantillaAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Delete, $"/api/consultasplantillas/{id}", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task ActivarConsultaPlantillaAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/consultasplantillas/{id}/activar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    public async Task DesactivarConsultaPlantillaAsync(int id, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, $"/api/consultasplantillas/{id}/desactivar", null, currentUserId, ip);
        await EnviarSinRetornoAsync(req);
    }

    // Configuracion Motor
    public async Task<ConfiguracionMotorConsultasDto?> GetConfiguracionMotorConsultasAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/configuracionmotorconsultas", null, currentUserId, ip);
            return await EnviarYLeerAsync<ConfiguracionMotorConsultasDto>(req);
        }
        catch { return null; }
    }

    public async Task<ConfiguracionMotorConsultasDto> ActualizarConfiguracionMotorConsultasAsync(ActualizarConfiguracionMotorConsultasRequest request, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Put, "/api/configuracionmotorconsultas", request, currentUserId, ip);
        return await EnviarYLeerAsync<ConfiguracionMotorConsultasDto>(req);
    }

    // ===== Motor de Herramientas (Tool Orchestrator) - ETAPA 11 =====
    public async Task<List<HerramientaDto>> GetHerramientasAsync(int? idAsistente, int currentUserId, string ip)
    {
        try
        {
            var qs = idAsistente.HasValue ? $"?idAsistente={idAsistente}" : string.Empty;
            var req = CrearRequest(HttpMethod.Get, $"/api/herramientas{qs}", null, currentUserId, ip);
            return await EnviarYLeerAsync<List<HerramientaDto>>(req);
        }
        catch { return new List<HerramientaDto>(); }
    }

    public async Task<HerramientaDto> CrearHerramientaAsync(CrearHerramientaRequest request, int currentUserId, string ip)
        => await EnviarYLeerAsync<HerramientaDto>(CrearRequest(HttpMethod.Post, "/api/herramientas", request, currentUserId, ip));

    public async Task ActualizarHerramientaAsync(int id, ActualizarHerramientaRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/herramientas/{id}", request, currentUserId, ip));

    public async Task ActivarHerramientaAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/herramientas/{id}/activar", null, currentUserId, ip));

    public async Task DesactivarHerramientaAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/herramientas/{id}/desactivar", null, currentUserId, ip));

    public async Task<List<HerramientaDto>> GetHerramientasDeAsistenteAsync(int idAsistente, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/asistenteherramientas/{idAsistente}/herramientas", null, currentUserId, ip);
            return await EnviarYLeerAsync<List<HerramientaDto>>(req);
        }
        catch { return new List<HerramientaDto>(); }
    }

    public async Task AsociarHerramientaAsync(int idAsistente, int idHerramienta, bool activa, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Post, $"/api/asistenteherramientas/{idAsistente}/herramientas/{idHerramienta}?activa={activa}", null, currentUserId, ip));

    public async Task DesasociarHerramientaAsync(int idAsistente, int idHerramienta, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Delete, $"/api/asistenteherramientas/{idAsistente}/herramientas/{idHerramienta}", null, currentUserId, ip));

    public async Task<List<EjecucionHerramientaDto>> GetEjecucionesHerramientasAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/ejecucionesherramientas", null, currentUserId, ip);
            return await EnviarYLeerAsync<List<EjecucionHerramientaDto>>(req);
        }
        catch { return new List<EjecucionHerramientaDto>(); }
    }

    public async Task<ConfiguracionOrchestratorDto> GetConfiguracionOrchestratorAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/orchestratorconfig", null, currentUserId, ip);
            return await EnviarYLeerAsync<ConfiguracionOrchestratorDto>(req);
        }
        catch { return new ConfiguracionOrchestratorDto(); }
    }

    public async Task GuardarConfiguracionOrchestratorAsync(ConfiguracionOrchestratorDto config, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, "/api/orchestratorconfig", config, currentUserId, ip));

    // ===== Motor de Workflows (Workflow Engine) - ETAPA 12 =====
    public async Task<List<WorkflowDto>> GetWorkflowsAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/workflows", null, currentUserId, ip);
            return await EnviarYLeerAsync<List<WorkflowDto>>(req);
        }
        catch { return new List<WorkflowDto>(); }
    }

    public async Task<WorkflowDto?> GetWorkflowByIdAsync(int id, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/workflows/{id}", null, currentUserId, ip);
            return await EnviarYLeerAsync<WorkflowDto>(req);
        }
        catch { return null; }
    }

    public async Task<WorkflowDto> CrearWorkflowAsync(CrearWorkflowRequest request, int currentUserId, string ip)
        => await EnviarYLeerAsync<WorkflowDto>(CrearRequest(HttpMethod.Post, "/api/workflows", request, currentUserId, ip));

    public async Task ActualizarWorkflowAsync(int id, ActualizarWorkflowRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/workflows/{id}", request, currentUserId, ip));

    public async Task ActivarWorkflowAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/workflows/{id}/activar", null, currentUserId, ip));

    public async Task DesactivarWorkflowAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/workflows/{id}/desactivar", null, currentUserId, ip));

    public async Task VersionarWorkflowAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/workflows/{id}/versionar", null, currentUserId, ip));

    public async Task EliminarWorkflowAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Delete, $"/api/workflows/{id}", null, currentUserId, ip));

    public async Task<List<WorkflowEjecucionDto>> GetWorkflowEjecucionesAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/workflowejecuciones", null, currentUserId, ip);
            return await EnviarYLeerAsync<List<WorkflowEjecucionDto>>(req);
        }
        catch { return new List<WorkflowEjecucionDto>(); }
    }

    public async Task<ConfiguracionWorkflowDto> GetConfiguracionWorkflowAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/configuracionworkflow", null, currentUserId, ip);
            return await EnviarYLeerAsync<ConfiguracionWorkflowDto>(req);
        }
        catch { return new ConfiguracionWorkflowDto(); }
    }

    public async Task GuardarConfiguracionWorkflowAsync(ConfiguracionWorkflowDto config, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, "/api/configuracionworkflow", config, currentUserId, ip));

    // Motor de Eventos Empresariales (Event Motor) - ETAPA 13
    public async Task<List<EventoEmpresarialDto>> GetEventosEmpresarialesAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<EventoEmpresarialDto>>(CrearRequest(HttpMethod.Get, "/api/eventosempresariales", null, currentUserId, ip)); }
        catch { return new List<EventoEmpresarialDto>(); }
    }

    public async Task<EventoEmpresarialDto?> GetEventoEmpresarialByIdAsync(int id, int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<EventoEmpresarialDto>(CrearRequest(HttpMethod.Get, $"/api/eventosempresariales/{id}", null, currentUserId, ip)); }
        catch { return null; }
    }

    public async Task<EventoEmpresarialDto> CrearEventoEmpresarialAsync(CrearEventoEmpresarialRequest request, int currentUserId, string ip)
        => await EnviarYLeerAsync<EventoEmpresarialDto>(CrearRequest(HttpMethod.Post, "/api/eventosempresariales", request, currentUserId, ip));

    public async Task ActualizarEventoEmpresarialAsync(int id, ActualizarEventoEmpresarialRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/eventosempresariales/{id}", request, currentUserId, ip));

    public async Task ActivarEventoEmpresarialAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/eventosempresariales/{id}/activar", null, currentUserId, ip));

    public async Task DesactivarEventoEmpresarialAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/eventosempresariales/{id}/desactivar", null, currentUserId, ip));

    public async Task EliminarEventoEmpresarialAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Delete, $"/api/eventosempresariales/{id}", null, currentUserId, ip));

    // Disparadores de Evento
    public async Task<List<DisparadorEventoDto>> GetDisparadoresEventoAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<DisparadorEventoDto>>(CrearRequest(HttpMethod.Get, "/api/disparadores-evento", null, currentUserId, ip)); }
        catch { return new List<DisparadorEventoDto>(); }
    }

    public async Task<DisparadorEventoDto?> GetDisparadorEventoByIdAsync(int id, int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<DisparadorEventoDto>(CrearRequest(HttpMethod.Get, $"/api/disparadores-evento/{id}", null, currentUserId, ip)); }
        catch { return null; }
    }

    public async Task<DisparadorEventoDto> CrearDisparadorEventoAsync(CrearDisparadorEventoRequest request, int currentUserId, string ip)
        => await EnviarYLeerAsync<DisparadorEventoDto>(CrearRequest(HttpMethod.Post, "/api/disparadores-evento", request, currentUserId, ip));

    public async Task ActualizarDisparadorEventoAsync(int id, ActualizarDisparadorEventoRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/disparadores-evento/{id}", request, currentUserId, ip));

    public async Task ActivarDisparadorEventoAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Post, $"/api/disparadores-evento/{id}/activar", null, currentUserId, ip));

    public async Task DesactivarDisparadorEventoAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Post, $"/api/disparadores-evento/{id}/desactivar", null, currentUserId, ip));

    public async Task EliminarDisparadorEventoAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Delete, $"/api/disparadores-evento/{id}", null, currentUserId, ip));

    public async Task<List<ReglaEventoDto>> GetReglasEventoAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<ReglaEventoDto>>(CrearRequest(HttpMethod.Get, "/api/reglasevento", null, currentUserId, ip)); }
        catch { return new List<ReglaEventoDto>(); }
    }

    public async Task<ReglaEventoDto> CrearReglaEventoAsync(CrearReglaEventoRequest request, int currentUserId, string ip)
        => await EnviarYLeerAsync<ReglaEventoDto>(CrearRequest(HttpMethod.Post, "/api/reglasevento", request, currentUserId, ip));

    public async Task ActualizarReglaEventoAsync(int id, ActualizarReglaEventoRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/reglasevento/{id}", request, currentUserId, ip));

    public async Task ActivarReglaEventoAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/reglasevento/{id}/activar", null, currentUserId, ip));

    public async Task DesactivarReglaEventoAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/reglasevento/{id}/desactivar", null, currentUserId, ip));

    public async Task EliminarReglaEventoAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Delete, $"/api/reglasevento/{id}", null, currentUserId, ip));

    public async Task<List<TareaProgramadaDto>> GetTareasProgramadasAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<TareaProgramadaDto>>(CrearRequest(HttpMethod.Get, "/api/tareasprogramadas", null, currentUserId, ip)); }
        catch { return new List<TareaProgramadaDto>(); }
    }

    public async Task<TareaProgramadaDto> CrearTareaProgramadaAsync(CrearTareaProgramadaRequest request, int currentUserId, string ip)
        => await EnviarYLeerAsync<TareaProgramadaDto>(CrearRequest(HttpMethod.Post, "/api/tareasprogramadas", request, currentUserId, ip));

    public async Task ActualizarTareaProgramadaAsync(int id, ActualizarTareaProgramadaRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/tareasprogramadas/{id}", request, currentUserId, ip));

    public async Task ActivarTareaProgramadaAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/tareasprogramadas/{id}/activar", null, currentUserId, ip));

    public async Task DesactivarTareaProgramadaAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/tareasprogramadas/{id}/desactivar", null, currentUserId, ip));

    public async Task EliminarTareaProgramadaAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Delete, $"/api/tareasprogramadas/{id}", null, currentUserId, ip));

    public async Task<List<EventoProcesadoDto>> GetEventosProcesadosAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<EventoProcesadoDto>>(CrearRequest(HttpMethod.Get, "/api/eventosprocesados", null, currentUserId, ip)); }
        catch { return new List<EventoProcesadoDto>(); }
    }

    public async Task<ConfiguracionEventoMotorDto> GetConfiguracionEventoMotorAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<ConfiguracionEventoMotorDto>(CrearRequest(HttpMethod.Get, "/api/configuracioneventomotor", null, currentUserId, ip)); }
        catch { return new ConfiguracionEventoMotorDto(); }
    }

    public async Task GuardarConfiguracionEventoMotorAsync(ConfiguracionEventoMotorDto config, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, "/api/configuracioneventomotor", config, currentUserId, ip));

    public async Task<MonitoreoEventosDto> GetMonitoreoEventosAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<MonitoreoEventosDto>(CrearRequest(HttpMethod.Get, "/api/monitoreoeventos/panel", null, currentUserId, ip)); }
        catch { return new MonitoreoEventosDto(); }
    }

    public async Task<EventoProcesadoDto> DispararEventoAsync(DispararEventoRequest request, int currentUserId, string ip)
        => await EnviarYLeerAsync<EventoProcesadoDto>(CrearRequest(HttpMethod.Post, "/api/eventomotor/disparar", request, currentUserId, ip));

    public async Task<IEnumerable<PermisoDto>> GetPermisosAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<PermisoDto>>(CrearRequest(HttpMethod.Get, "/api/seguridad/permisos", null, currentUserId, ip)); }
        catch { return new List<PermisoDto>(); }
    }

    public async Task<IEnumerable<PoliticaIADto>> GetPoliticasAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<PoliticaIADto>>(CrearRequest(HttpMethod.Get, "/api/seguridad/politicas", null, currentUserId, ip)); }
        catch { return new List<PoliticaIADto>(); }
    }

    public async Task<DashboardSeguridadDto> GetDashboardSeguridadAsync(int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<DashboardSeguridadDto>(CrearRequest(HttpMethod.Get, "/api/seguridad/dashboard", null, currentUserId, ip)); }
        catch { return new DashboardSeguridadDto(); }
    }

    public async Task<List<AsistenteAutorizadoDto>> GetAsistentesDeUsuarioAsync(int idUsuario, int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<AsistenteAutorizadoDto>>(CrearRequest(HttpMethod.Get, $"/api/seguridad/usuarios/{idUsuario}/asistentes", null, currentUserId, ip)); }
        catch { return new List<AsistenteAutorizadoDto>(); }
    }

    public async Task<List<FuenteAutorizadaDto>> GetFuentesDeUsuarioAsync(int idUsuario, int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<FuenteAutorizadaDto>>(CrearRequest(HttpMethod.Get, $"/api/seguridad/usuarios/{idUsuario}/fuentes", null, currentUserId, ip)); }
        catch { return new List<FuenteAutorizadaDto>(); }
    }

    public async Task AsignarAsistentesUsuarioAsync(int idUsuario, AsignarAsistentesUsuarioRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Post, $"/api/seguridad/usuarios/{idUsuario}/asistentes", request, currentUserId, ip));

    public async Task AsignarFuentesUsuarioAsync(int idUsuario, AsignarFuentesUsuarioRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Post, $"/api/seguridad/usuarios/{idUsuario}/fuentes", request, currentUserId, ip));

    public async Task<List<PermisoAsignadoDto>> GetPermisosDeRolAsync(int idRol, int currentUserId, string ip)
    {
        try { return await EnviarYLeerAsync<List<PermisoAsignadoDto>>(CrearRequest(HttpMethod.Get, $"/api/seguridad/roles/{idRol}/permisos", null, currentUserId, ip)); }
        catch { return new List<PermisoAsignadoDto>(); }
    }

    public async Task AsignarPermisosRolAsync(int idRol, AsignarPermisosRolRequest request, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Post, $"/api/seguridad/roles/{idRol}/permisos", request, currentUserId, ip));

    // ===== Agent Orchestrator (ETAPA 17) =====
    public async Task<AgentExecutionResultDto?> ExecuteOrchestratorAsync(int idAgentePrincipal, string pregunta, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Post, "/api/orchestrator/execute",
                new { IdAgentePrincipal = idAgentePrincipal, Pregunta = pregunta }, currentUserId, ip);
            return await EnviarYLeerAsync<AgentExecutionResultDto>(req);
        }
        catch { return null; }
    }

    public async Task<object?> GetOrchestratorDashboardAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/orchestrator/dashboard", null, currentUserId, ip);
            return await EnviarYLeerAsync<object>(req);
        }
        catch { return null; }
    }

    public async Task<object?> GetOrchestratorTrazasAsync(int idExecution, int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, $"/api/orchestrator/trazas/{idExecution}", null, currentUserId, ip);
            return await EnviarYLeerAsync<object>(req);
        }
        catch { return null; }
    }

    public async Task<List<AgentCollaborationRuleDto>> GetReglasColaboracionAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/orchestrator/reglas", null, currentUserId, ip);
            return await EnviarYLeerAsync<List<AgentCollaborationRuleDto>>(req);
        }
        catch { return new List<AgentCollaborationRuleDto>(); }
    }

    public async Task<AgentCollaborationRuleDto> CrearReglaColaboracionAsync(AgentCollaborationRuleDto regla, int currentUserId, string ip)
    {
        var req = CrearRequest(HttpMethod.Post, "/api/orchestrator/reglas", regla, currentUserId, ip);
        return await EnviarYLeerAsync<AgentCollaborationRuleDto>(req);
    }

    public async Task ActualizarReglaColaboracionAsync(int id, AgentCollaborationRuleDto regla, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Put, $"/api/orchestrator/reglas/{id}", regla, currentUserId, ip));

    public async Task EliminarReglaColaboracionAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Delete, $"/api/orchestrator/reglas/{id}", null, currentUserId, ip));

    public async Task<List<AgenteSimpleDto>> GetAgentesParaOrquestadorAsync(int currentUserId, string ip)
    {
        try
        {
            var req = CrearRequest(HttpMethod.Get, "/api/orchestrator/agentes", null, currentUserId, ip);
            return await EnviarYLeerAsync<List<AgenteSimpleDto>>(req);
        }
        catch { return new List<AgenteSimpleDto>(); }
    }

    // ===== Planner Engine (ETAPA 18) =====
    public async Task<PlanDto> GenerarPlanAsync(string objetivo, int currentUserId, string ip)
        => await EnviarYLeerAsync<PlanDto>(CrearRequest(HttpMethod.Post, "/api/planner/generar",
            new { objetivo }, currentUserId, ip));

    public async Task<ResultadoValidacionPlanDto> ValidarPlanAsync(int id, int currentUserId, string ip)
        => await EnviarYLeerAsync<ResultadoValidacionPlanDto>(CrearRequest(HttpMethod.Post,
            $"/api/planner/validar/{id}", null, currentUserId, ip));

    public async Task<SimulacionPlanDto> SimularPlanAsync(int id, int currentUserId, string ip)
        => await EnviarYLeerAsync<SimulacionPlanDto>(CrearRequest(HttpMethod.Get,
            $"/api/planner/simular/{id}", null, currentUserId, ip));

    public async Task<PlanDto> EjecutarPlanAsync(int id, int currentUserId, string ip)
        => await EnviarYLeerAsync<PlanDto>(CrearRequest(HttpMethod.Post,
            $"/api/planner/ejecutar/{id}", null, currentUserId, ip));

    public async Task AprobarPlanAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Post, $"/api/planner/aprobar/{id}", null, currentUserId, ip));

    public async Task CancelarPlanAsync(int id, int currentUserId, string ip)
        => await EnviarSinRetornoAsync(CrearRequest(HttpMethod.Post, $"/api/planner/cancelar/{id}", null, currentUserId, ip));

    public async Task<PlannerDashboardDto> GetPlannerDashboardAsync(int currentUserId, string ip)
        => await EnviarYLeerAsync<PlannerDashboardDto>(CrearRequest(HttpMethod.Get, "/api/planner/dashboard", null, currentUserId, ip));

    public async Task<PlanDto> GetPlanAsync(int id, int currentUserId, string ip)
        => await EnviarYLeerAsync<PlanDto>(CrearRequest(HttpMethod.Get, $"/api/planner/{id}", null, currentUserId, ip));

    // Centro de Aprobaciones / Human-in-the-Loop (ETAPA 19)
    public async Task<JsonElement> GetAprobacionesDashboardAsync(int currentUserId, string ip)
        => await EnviarYLeerAsync<JsonElement>(CrearRequest(HttpMethod.Get, "/api/aprobaciones/dashboard", null, currentUserId, ip));

    public async Task<JsonElement> GetBandejaAprobacionesAsync(int currentUserId, string ip)
        => await EnviarYLeerAsync<JsonElement>(CrearRequest(HttpMethod.Get, "/api/aprobaciones/bandeja", null, currentUserId, ip));

    public async Task<JsonElement> DecidirAprobacionAsync(int id, string decision, string? comentario, int currentUserId, string ip)
        => await EnviarYLeerAsync<JsonElement>(CrearRequest(HttpMethod.Post, $"/api/aprobaciones/{id}/decidir",
            new { decision, comentario }, currentUserId, ip));

    public async Task<JsonElement> DelegarAprobacionAsync(int id, int idUsuarioDestino, string? comentario, int currentUserId, string ip)
        => await EnviarYLeerAsync<JsonElement>(CrearRequest(HttpMethod.Post, $"/api/aprobaciones/{id}/delegar",
            new { idUsuarioDestino, comentario }, currentUserId, ip));
}
