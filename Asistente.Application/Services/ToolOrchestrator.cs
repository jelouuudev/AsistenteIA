using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

/// <summary>
/// Implementación del Motor de Herramientas (Tool Orchestrator).
/// Registra, descubre, resuelve y ejecuta herramientas bajo controles de autorización,
/// registrando auditoría de toda ejecución (éxito o fallo).
/// </summary>
public class ToolOrchestrator : IToolOrchestrator
{
    private readonly IEnumerable<ITool> _herramientas;
    private readonly IHerramientaRepository _herramientaRepository;
    private readonly IAsistenteHerramientaRepository _asistenteHerramientaRepository;
    private readonly IEjecucionHerramientaRepository _ejecucionRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IConfiguracionOrchestratorRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ToolOrchestrator> _logger;
    private readonly IAutorizacionService _autorizacionService;

    public ToolOrchestrator(
        IEnumerable<ITool> herramientas,
        IHerramientaRepository herramientaRepository,
        IAsistenteHerramientaRepository asistenteHerramientaRepository,
        IEjecucionHerramientaRepository ejecucionRepository,
        IUsuarioRepository usuarioRepository,
        IConfiguracionOrchestratorRepository configRepository,
        IUnitOfWork unitOfWork,
        ILogger<ToolOrchestrator> logger,
        IAutorizacionService autorizacionService)
    {
        _herramientas = herramientas;
        _herramientaRepository = herramientaRepository;
        _asistenteHerramientaRepository = asistenteHerramientaRepository;
        _ejecucionRepository = ejecucionRepository;
        _usuarioRepository = usuarioRepository;
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _autorizacionService = autorizacionService;
    }

    public async Task<IEnumerable<Herramienta>> DescubrirHerramientasAsync(CancellationToken cancellationToken = default)
        => await _herramientaRepository.GetActivasAsync();

    public async Task<IEnumerable<Herramienta>> ObtenerHerramientasParaAsistenteAsync(int idAsistente, CancellationToken cancellationToken = default)
        => await _asistenteHerramientaRepository.GetHerramientasPorAsistenteAsync(idAsistente);

    public async Task<ToolExecutionResult> EjecutarAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var inicio = DateTime.UtcNow;

        // Resolver herramienta por código
        var herramienta = await _herramientaRepository.GetByCodigoAsync(request.HerramientaCodigo);
        var codigo = request.HerramientaCodigo;

        if (herramienta == null)
        {
            await RegistrarAuditoriaAsync(null, request, "Rechazada", "Herramienta no registrada.", inicio);
            return new ToolExecutionResult { Exitoso = false, Error = $"La herramienta '{codigo}' no está registrada." };
        }

        // Validar estado de la herramienta
        if (!herramienta.Activa)
        {
            await RegistrarAuditoriaAsync(herramienta, request, "Rechazada", "La herramienta está desactivada.", inicio);
            return new ToolExecutionResult { Exitoso = false, Error = "La herramienta está desactivada." };
        }

        // Validar permisos (usuario autenticado, asociación al asistente, política)
        var (autorizado, motivo) = await ValidarPermisosAsync(request, herramienta);
        if (!autorizado)
        {
            await RegistrarAuditoriaAsync(herramienta, request, "Rechazada", motivo, inicio);
            return new ToolExecutionResult { Exitoso = false, Error = motivo };
        }

        // Resolver implementación
        var implementacion = _herramientas.FirstOrDefault(t => t.Name == herramienta.Codigo);
        if (implementacion == null)
        {
            await RegistrarAuditoriaAsync(herramienta, request, "Error", "No hay implementación para la herramienta.", inicio);
            return new ToolExecutionResult { Exitoso = false, Error = "No hay implementación disponible para la herramienta." };
        }

        try
        {
            var config = await _configRepository.GetAsync();
            var timeout = config?.TiempoMaximoEjecucionMs ?? 30000;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(timeout));

            _logger.LogInformation("Ejecutando herramienta '{Codigo}' para usuario {Usuario}.", herramienta.Codigo, request.UsuarioNombre);
            var resultado = await implementacion.ExecuteAsync(request, cts.Token);

            await RegistrarAuditoriaAsync(herramienta, request,
                resultado.Exitoso ? "Exitosa" : "Error",
                resultado.Exitoso ? (resultado.Contenido ?? string.Empty)[..Math.Min((resultado.Contenido ?? string.Empty).Length, 4000)] : (resultado.Error ?? "Error desconocido"),
                inicio);

            return resultado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al ejecutar la herramienta '{Codigo}'.", herramienta.Codigo);
            await RegistrarAuditoriaAsync(herramienta, request, "Error", ex.Message, inicio);
            return new ToolExecutionResult { Exitoso = false, Error = $"Error al ejecutar la herramienta: {ex.Message}" };
        }
    }

    private async Task<(bool Autorizado, string? Motivo)> ValidarPermisosAsync(ToolExecutionRequest request, Herramienta herramienta)
    {
        if (request.IdUsuario <= 0)
            return (false, "Usuario no autenticado.");

        // Validación de permiso por herramienta (ETAPA 14 - Casos 1 y 3).
        var auth = await _autorizacionService.VerificarHerramientaAsync(
            request.IdUsuario, request.IdAsistente ?? 0, herramienta.Codigo);
        if (!auth.Permitido)
            return (false, auth.Motivo);

        var config = await _configRepository.GetAsync();
        if (config != null && config.RequiereAutorizacion && herramienta.RequierePermiso)
        {
            if (request.IdAsistente.HasValue)
            {
                var relacion = await _asistenteHerramientaRepository.GetAsync(request.IdAsistente.Value, herramienta.IdHerramienta);
                if (relacion == null || !relacion.Activa)
                    return (false, "La herramienta no está autorizada para este asistente.");
            }
            else
            {
                // Sin asistente: requerir que el usuario tenga rol Administrador
                var usuario = await _usuarioRepository.GetByIdAsync(request.IdUsuario);
                var esAdmin = usuario?.UsuarioRoles.Any(ur => ur.Rol?.Nombre == "Administrador") ?? false;
                if (!esAdmin)
                    return (false, "Se requiere un asistente con la herramienta autorizada.");
            }
        }

        return (true, null);
    }

    private async Task RegistrarAuditoriaAsync(Herramienta? herramienta, ToolExecutionRequest request, string estado, string resultado, DateTime inicio)
    {
        try
        {
            var parametrosJson = System.Text.Json.JsonSerializer.Serialize(request.Parametros);
            var ejecucion = new EjecucionHerramienta
            {
                IdHerramienta = herramienta?.IdHerramienta ?? 0,
                IdUsuario = request.IdUsuario,
                FechaHora = inicio,
                Parametros = parametrosJson,
                Resultado = resultado,
                TiempoEjecucion = (long)(DateTime.UtcNow - inicio).TotalMilliseconds,
                Estado = estado
            };
            await _ejecucionRepository.AddAsync(ejecucion);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar la auditoría de la ejecución de herramienta.");
        }
    }
}
