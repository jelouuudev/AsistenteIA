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

    public DashboardSeguridadService(
        IUsuarioRepository usuarioRepository,
        IConversacionRepository conversacionRepository,
        IAuditoriaActividadRepository auditoriaRepository,
        IAuditoriaIARepository auditoriaIARepository,
        IEventoProcesadoRepository eventoProcesadoRepository)
    {
        _usuarioRepository = usuarioRepository;
        _conversacionRepository = conversacionRepository;
        _auditoriaRepository = auditoriaRepository;
        _auditoriaIARepository = auditoriaIARepository;
        _eventoProcesadoRepository = eventoProcesadoRepository;
    }

    public async Task<DashboardSeguridadDto> ObtenerAsync(CancellationToken ct = default)
    {
        var usuarios = await _usuarioRepository.GetAllAsync();
        var conversaciones = await _conversacionRepository.GetAllAsync();
        var eventos = await _eventoProcesadoRepository.GetAllAsync(ct);
        var auditoriasRecientes = (await _auditoriaRepository.GetAllAsync(0, 50, ct)).Select(Map).ToList();

        var consultas = await _auditoriaRepository.CountByFiltroAsync(modulo: "Chat", accion: "Pregunta", ct: ct);
        var usoHerramientas = await _auditoriaRepository.CountByFiltroAsync(modulo: "Herramientas", ct: ct);
        var consultasSql = await _auditoriaRepository.CountByFiltroAsync(modulo: "SQL", ct: ct);
        var workflows = await _auditoriaRepository.CountByFiltroAsync(modulo: "Workflows", ct: ct);
        var errores = await _auditoriaRepository.CountByFiltroAsync(resultado: "Error", ct: ct);

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
