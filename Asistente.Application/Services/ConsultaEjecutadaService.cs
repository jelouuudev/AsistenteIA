using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class ConsultaEjecutadaService : IConsultaEjecutadaService
{
    private readonly IConsultaEjecutadaRepository _consultaRepository;
    private readonly IConexionBaseDatosRepository _conexionRepository;
    private readonly ITablaAutorizadaRepository _tablaRepository;
    private readonly IVistaAutorizadaRepository _vistaRepository;
    private readonly IConsultaPlantillaRepository _plantillaRepository;
    private readonly ILogger<ConsultaEjecutadaService> _logger;

    public ConsultaEjecutadaService(
        IConsultaEjecutadaRepository consultaRepository,
        IConexionBaseDatosRepository conexionRepository,
        ITablaAutorizadaRepository tablaRepository,
        IVistaAutorizadaRepository vistaRepository,
        IConsultaPlantillaRepository plantillaRepository,
        ILogger<ConsultaEjecutadaService> logger)
    {
        _consultaRepository = consultaRepository;
        _conexionRepository = conexionRepository;
        _tablaRepository = tablaRepository;
        _vistaRepository = vistaRepository;
        _plantillaRepository = plantillaRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ConsultaEjecutadaDto>> ObtenerTodasAsync(CancellationToken cancellationToken = default)
    {
        var consultas = await _consultaRepository.GetAllAsync();
        return consultas.Select(MapearADto);
    }

    public async Task<IEnumerable<ConsultaEjecutadaDto>> ObtenerPorUsuarioAsync(int idUsuario, CancellationToken cancellationToken = default)
    {
        var consultas = await _consultaRepository.GetByUsuarioIdAsync(idUsuario);
        return consultas.Select(MapearADto);
    }

    public async Task<IEnumerable<ConsultaEjecutadaDto>> ObtenerPorConexionAsync(int idConexion, CancellationToken cancellationToken = default)
    {
        var consultas = await _consultaRepository.GetByConexionIdAsync(idConexion);
        return consultas.Select(MapearADto);
    }

    public async Task<ConsultaEjecutadaDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var consulta = await _consultaRepository.GetByIdAsync(id);
        return consulta == null ? null : MapearADto(consulta);
    }

    public async Task<DashboardConsultasDto> ObtenerDashboardAsync(CancellationToken cancellationToken = default)
    {
        var consultas = (await _consultaRepository.GetAllAsync()).ToList();
        var conexiones = (await _conexionRepository.GetActivasAsync()).ToList();
        var tablas = new List<TablaAutorizada>();
        var vistas = new List<VistaAutorizada>();
        var plantillas = (await _plantillaRepository.GetAllAsync()).ToList();

        foreach (var c in conexiones)
        {
            tablas.AddRange(await _tablaRepository.GetActivasByConexionIdAsync(c.IdConexion));
            vistas.AddRange(await _vistaRepository.GetActivasByConexionIdAsync(c.IdConexion));
        }

        var completadas = consultas.Count(c => c.Estado == nameof(EstadoConsulta.Completada));
        var errores = consultas.Count(c => c.Estado == nameof(EstadoConsulta.Error));
        var bloqueadas = consultas.Count(c => c.Estado == nameof(EstadoConsulta.Bloqueada));

        return new DashboardConsultasDto
        {
            TotalConsultas = consultas.Count,
            TotalCompletadas = completadas,
            TotalErrores = errores,
            TotalBloqueadas = bloqueadas,
            PromedioTiempoMs = consultas.Count > 0 ? consultas.Average(c => c.TiempoEjecucion) : 0,
            TotalRegistros = consultas.Sum(c => c.CantidadRegistros),
            TotalConexionesActivas = conexiones.Count,
            TotalTablasAutorizadas = tablas.Count,
            TotalVistasAutorizadas = vistas.Count,
            TotalPlantillas = plantillas.Count,
            UltimasConsultas = consultas.Take(10).Select(MapearADto).ToList()
        };
    }

    private static ConsultaEjecutadaDto MapearADto(ConsultaEjecutada c)
    {
        return new ConsultaEjecutadaDto
        {
            IdConsulta = c.IdConsulta,
            IdUsuario = c.IdUsuario,
            NombreUsuario = c.Usuario?.UsuarioNombre ?? c.Usuario?.Correo,
            IdConexion = c.IdConexion,
            NombreConexion = c.Conexion?.Nombre,
            FechaHora = c.FechaHora,
            PreguntaUsuario = c.PreguntaUsuario,
            OperacionEjecutada = c.OperacionEjecutada,
            ConsultaGenerada = c.ConsultaGenerada,
            TiempoEjecucion = c.TiempoEjecucion,
            CantidadRegistros = c.CantidadRegistros,
            Estado = c.Estado,
            Resultado = c.Resultado
        };
    }
}
