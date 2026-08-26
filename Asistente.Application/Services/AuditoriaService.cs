using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly IAuditoriaRepository _auditoriaRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AuditoriaService(IAuditoriaRepository auditoriaRepository, IUnitOfWork unitOfWork)
    {
        _auditoriaRepository = auditoriaRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AuditoriaSesionDto>> ObtenerSesionesAsync()
    {
        var sesiones = await _auditoriaRepository.GetAllSesionesAsync();
        return sesiones.Select(s => new AuditoriaSesionDto
        {
            IdSesion = s.IdSesion,
            IdUsuario = s.IdUsuario,
            UsuarioNombre = s.Usuario?.UsuarioNombre ?? "Desconocido",
            FechaInicio = s.FechaInicio,
            FechaFin = s.FechaFin,
            DireccionIP = s.DireccionIP,
            Navegador = s.Navegador,
            Estado = s.Estado
        });
    }

    public async Task<IEnumerable<AuditoriaActividadDto>> ObtenerActividadesAsync()
    {
        var actividades = await _auditoriaRepository.GetAllActividadesAsync();
        return actividades.Select(a => new AuditoriaActividadDto
        {
            IdActividad = a.IdActividad,
            IdUsuario = a.IdUsuario,
            UsuarioNombre = a.Usuario?.UsuarioNombre ?? "Desconocido",
            FechaHora = a.FechaHora,
            Modulo = a.Modulo,
            Accion = a.Accion,
            Descripcion = a.Descripcion,
            DireccionIP = a.DireccionIP
        });
    }

    public async Task RegistrarActividadAsync(int userId, string modulo, string accion, string descripcion, string? ipAddress)
    {
        var actividad = new AuditoriaActividad
        {
            IdUsuario = userId,
            FechaHora = DateTime.UtcNow,
            Modulo = modulo,
            Accion = accion,
            Descripcion = descripcion,
            DireccionIP = ipAddress
        };

        await _auditoriaRepository.AddActividadAsync(actividad);
        await _unitOfWork.SaveChangesAsync();
    }
}
