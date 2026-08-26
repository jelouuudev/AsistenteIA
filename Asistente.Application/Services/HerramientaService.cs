using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class HerramientaService : IHerramientaService
{
    private readonly IHerramientaRepository _herramientaRepository;
    private readonly IAsistenteHerramientaRepository _asistenteHerramientaRepository;
    private readonly IEjecucionHerramientaRepository _ejecucionRepository;
    private readonly IConfiguracionOrchestratorRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;

    public HerramientaService(
        IHerramientaRepository herramientaRepository,
        IAsistenteHerramientaRepository asistenteHerramientaRepository,
        IEjecucionHerramientaRepository ejecucionRepository,
        IConfiguracionOrchestratorRepository configRepository,
        IUnitOfWork unitOfWork)
    {
        _herramientaRepository = herramientaRepository;
        _asistenteHerramientaRepository = asistenteHerramientaRepository;
        _ejecucionRepository = ejecucionRepository;
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<HerramientaDto>> ObtenerTodasAsync(int? idAsistente = null)
    {
        var herramientas = (await _herramientaRepository.GetAllAsync()).ToList();
        var asociadas = idAsistente.HasValue
            ? (await _asistenteHerramientaRepository.GetHerramientasPorAsistenteAsync(idAsistente.Value))
                .Select(h => h.IdHerramienta).ToHashSet()
            : null;

        var dtos = new List<HerramientaDto>();
        foreach (var h in herramientas)
        {
            var stats = await _ejecucionRepository.GetEstadisticasAsync(h.IdHerramienta);
            dtos.Add(new HerramientaDto
            {
                IdHerramienta = h.IdHerramienta,
                Nombre = h.Nombre,
                Codigo = h.Codigo,
                Descripcion = h.Descripcion,
                Categoria = h.Categoria,
                Activa = h.Activa,
                RequierePermiso = h.RequierePermiso,
                FechaRegistro = h.FechaRegistro,
                TotalEjecuciones = stats.Total,
                TiempoPromedioMs = Math.Round(stats.TiempoPromedio, 1),
                Errores = stats.Errores,
                UltimaEjecucion = stats.Ultima,
                AsociadaAlAsistente = asociadas?.Contains(h.IdHerramienta) ?? false
            });
        }
        return dtos;
    }

    public async Task<HerramientaDto?> ObtenerPorIdAsync(int id)
    {
        var h = await _herramientaRepository.GetByIdAsync(id);
        if (h == null) return null;
        var stats = await _ejecucionRepository.GetEstadisticasAsync(h.IdHerramienta);
        return new HerramientaDto
        {
            IdHerramienta = h.IdHerramienta,
            Nombre = h.Nombre,
            Codigo = h.Codigo,
            Descripcion = h.Descripcion,
            Categoria = h.Categoria,
            Activa = h.Activa,
            RequierePermiso = h.RequierePermiso,
            FechaRegistro = h.FechaRegistro,
            TotalEjecuciones = stats.Total,
            TiempoPromedioMs = Math.Round(stats.TiempoPromedio, 1),
            Errores = stats.Errores,
            UltimaEjecucion = stats.Ultima
        };
    }

    public async Task<HerramientaDto> CrearAsync(CrearHerramientaRequest request)
    {
        var existente = await _herramientaRepository.GetByCodigoAsync(request.Codigo);
        if (existente != null)
            throw new InvalidOperationException($"Ya existe una herramienta con el código '{request.Codigo}'.");

        var herramienta = new Herramienta
        {
            Nombre = request.Nombre,
            Codigo = request.Codigo,
            Descripcion = request.Descripcion,
            Categoria = request.Categoria,
            Activa = request.Activa,
            RequierePermiso = request.RequierePermiso,
            FechaRegistro = DateTime.UtcNow
        };
        await _herramientaRepository.AddAsync(herramienta);
        await _unitOfWork.SaveChangesAsync();

        return await ObtenerPorIdAsync(herramienta.IdHerramienta)
            ?? throw new InvalidOperationException("No se pudo recuperar la herramienta creada.");
    }

    public async Task ActualizarAsync(int id, ActualizarHerramientaRequest request)
    {
        var h = await _herramientaRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Herramienta con ID {id} no encontrada.");
        h.Nombre = request.Nombre;
        h.Descripcion = request.Descripcion;
        h.Categoria = request.Categoria;
        h.Activa = request.Activa;
        h.RequierePermiso = request.RequierePermiso;
        await _herramientaRepository.UpdateAsync(h);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task EliminarAsync(int id)
    {
        var h = await _herramientaRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Herramienta con ID {id} no encontrada.");
        // Borrar primero las relaciones asistente-herramienta para no violar FK.
        var relaciones = await _asistenteHerramientaRepository.GetHerramientasPorAsistenteAsync(id);
        // Nota: GetHerramientasPorAsistenteAsync devuelve herramientas del asistente; para
        // borrar TODAS las relaciones de esta herramienta usamos el repo de relaciones.
        var relacionesDeHerramienta = (await _asistenteHerramientaRepository.ObtenerPorHerramientaAsync(id));
        foreach (var r in relacionesDeHerramienta)
            await _asistenteHerramientaRepository.DeleteAsync(r);
        await _herramientaRepository.DeleteByIdAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task ActivarAsync(int id)
    {
        var h = await _herramientaRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Herramienta con ID {id} no encontrada.");
        h.Activa = true;
        await _herramientaRepository.UpdateAsync(h);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DesactivarAsync(int id)
    {
        var h = await _herramientaRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Herramienta con ID {id} no encontrada.");
        h.Activa = false;
        await _herramientaRepository.UpdateAsync(h);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<HerramientaDto>> ObtenerAsociadasAlAsistenteAsync(int idAsistente)
    {
        var herramientas = await _asistenteHerramientaRepository.GetHerramientasPorAsistenteAsync(idAsistente);
        return herramientas.Select(h => new HerramientaDto
        {
            IdHerramienta = h.IdHerramienta,
            Nombre = h.Nombre,
            Codigo = h.Codigo,
            Descripcion = h.Descripcion,
            Categoria = h.Categoria,
            Activa = h.Activa,
            RequierePermiso = h.RequierePermiso,
            FechaRegistro = h.FechaRegistro,
            AsociadaAlAsistente = true
        });
    }

    public async Task AsociarHerramientaAsync(int idAsistente, int idHerramienta, bool activa)
    {
        var existente = await _asistenteHerramientaRepository.GetAsync(idAsistente, idHerramienta);
        if (existente != null)
        {
            existente.Activa = activa;
            await _asistenteHerramientaRepository.UpdateAsync(existente);
        }
        else
        {
            await _asistenteHerramientaRepository.AddAsync(new AsistenteHerramienta
            {
                IdAsistente = idAsistente,
                IdHerramienta = idHerramienta,
                Activa = activa
            });
        }
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DesasociarHerramientaAsync(int idAsistente, int idHerramienta)
    {
        var existente = await _asistenteHerramientaRepository.GetAsync(idAsistente, idHerramienta);
        if (existente != null)
        {
            await _asistenteHerramientaRepository.DeleteAsync(existente);
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task<ConfiguracionOrchestratorDto> ObtenerConfiguracionAsync()
    {
        var config = await _configRepository.GetAsync();
        return new ConfiguracionOrchestratorDto
        {
            Habilitado = config?.Habilitado ?? true,
            Prioridad = config?.Prioridad ?? 100,
            TiempoMaximoEjecucionMs = config?.TiempoMaximoEjecucionMs ?? 30000,
            MaxEjecucionesSimultaneas = config?.MaxEjecucionesSimultaneas ?? 4,
            RequiereAutorizacion = config?.RequiereAutorizacion ?? true
        };
    }

    public async Task GuardarConfiguracionAsync(ConfiguracionOrchestratorDto config)
    {
        await _configRepository.UpdateAsync(new ConfiguracionOrchestrator
        {
            Habilitado = config.Habilitado,
            Prioridad = config.Prioridad,
            TiempoMaximoEjecucionMs = config.TiempoMaximoEjecucionMs,
            MaxEjecucionesSimultaneas = config.MaxEjecucionesSimultaneas,
            RequiereAutorizacion = config.RequiereAutorizacion
        });
        await _unitOfWork.SaveChangesAsync();
    }
}
