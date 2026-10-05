using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services.Seguridad;

/// <summary>
/// Dashboard administrativo de observabilidad (Actividad 14) y métricas de IA (Actividad 15).
/// </summary>
public class DashboardSeguridadService : IDashboardSeguridadService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IConversacionRepository _conversacionRepository;
    private readonly IAuditoriaActividadRepository _auditoriaRepository;
    private readonly IAuditoriaIARepository _auditoriaIARepository;
    private readonly IEventoProcesadoRepository _eventoProcesadoRepository;
    private readonly IMensajeRepository _mensajeRepository;
    private readonly IEjecucionHerramientaRepository _ejecucionHerramientaRepository;
    private readonly IWorkflowEjecucionRepository _workflowEjecucionRepository;

    public DashboardSeguridadService(
        IUsuarioRepository usuarioRepository,
        IConversacionRepository conversacionRepository,
        IAuditoriaActividadRepository auditoriaRepository,
        IAuditoriaIARepository auditoriaIARepository,
        IEventoProcesadoRepository eventoProcesadoRepository,
        IMensajeRepository mensajeRepository,
        IEjecucionHerramientaRepository ejecucionHerramientaRepository,
        IWorkflowEjecucionRepository workflowEjecucionRepository)
    {
        _usuarioRepository = usuarioRepository;
        _conversacionRepository = conversacionRepository;
        _auditoriaRepository = auditoriaRepository;
        _auditoriaIARepository = auditoriaIARepository;
        _eventoProcesadoRepository = eventoProcesadoRepository;
        _mensajeRepository = mensajeRepository;
        _ejecucionHerramientaRepository = ejecucionHerramientaRepository;
        _workflowEjecucionRepository = workflowEjecucionRepository;
    }

    public async Task<DashboardSeguridadDto> ObtenerAsync(CancellationToken ct = default)
    {
        var usuarios = await _usuarioRepository.GetAllAsync();
        var conversaciones = await _conversacionRepository.GetAllAsync();
        var eventos = (await _eventoProcesadoRepository.GetAllAsync(ct)).ToList();
        var auditoriasRecientes = (await _auditoriaRepository.GetAllAsync(0, 50, ct)).Select(Map).ToList();

        // Contadores desde las tablas reales (antes leían módulos de auditoría
        // que ningún flujo registraba y siempre daban 0).
        var consultas = await _mensajeRepository.CountByRolAsync(Asistente.Domain.Enums.RolMensaje.User, ct);
        var (usoHerramientas, consultasSql) = await _ejecucionHerramientaRepository.ContarAsync(ct);
        var workflows = await _workflowEjecucionRepository.CountAsync(ct);
        var erroresAuditoria = await _auditoriaRepository.CountByFiltroAsync(resultado: "Error", ct: ct);
        var errores = erroresAuditoria + eventos.Count(e => e.Estado == "Error");

        var iaRecientes = await _auditoriaIARepository.GetRecientesAsync(200, ct);
        var tiempoPromedio = iaRecientes.Any() ? iaRecientes.Average(a => a.TiempoRespuestaMs) : 0;

        return new DashboardSeguridadDto
        {
            UsuariosActivos = usuarios.Count(u => u.Activo),
            Conversaciones = conversaciones.Count(),
            ConsultasRealizadas = (int)consultas,
            UsoHerramientas = (int)usoHerramientas,
            ConsultasSql = (int)consultasSql,
            WorkflowsEjecutados = (int)workflows,
            EventosProcesados = eventos.Count(),
            Errores = (int)errores,
            TiempoPromedioRespuestaMs = System.Math.Round(tiempoPromedio, 1),
            UltimasAuditorias = auditoriasRecientes
        };
    }

    private static AuditoriaActividadDto Map(AuditoriaActividad a) => new()
    {
        IdActividad = a.IdActividad,
        IdUsuario = a.IdUsuario,
        UsuarioNombre = a.Usuario?.UsuarioNombre ?? "Desconocido",
        FechaHora = a.FechaHora,
        TipoOperacion = a.TipoOperacion,
        Modulo = a.Modulo,
        Accion = a.Accion,
        Resultado = a.Resultado,
        Descripcion = a.Descripcion,
        DireccionIP = a.DireccionIP
    };
}
