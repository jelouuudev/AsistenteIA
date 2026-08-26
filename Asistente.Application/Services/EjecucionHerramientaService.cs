using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;

namespace Asistente.Application.Services;

public class EjecucionHerramientaService : IEjecucionHerramientaService
{
    private readonly IEjecucionHerramientaRepository _repository;

    public EjecucionHerramientaService(IEjecucionHerramientaRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<EjecucionHerramientaDto>> ObtenerTodasAsync()
    {
        var ejecuciones = await _repository.GetAllAsync(300);
        return ejecuciones.Select(e => new EjecucionHerramientaDto
        {
            IdEjecucion = e.IdEjecucion,
            IdHerramienta = e.IdHerramienta,
            CodigoHerramienta = e.Herramienta?.Codigo ?? "—",
            NombreHerramienta = e.Herramienta?.Nombre ?? "—",
            IdUsuario = e.IdUsuario,
            UsuarioNombre = e.Usuario?.UsuarioNombre ?? "Desconocido",
            FechaHora = e.FechaHora,
            Parametros = e.Parametros,
            Resultado = e.Resultado,
            TiempoEjecucion = e.TiempoEjecucion,
            Estado = e.Estado
        });
    }

    public async Task<EjecucionHerramientaDto?> ObtenerPorIdAsync(int id)
    {
        var todas = await ObtenerTodasAsync();
        return todas.FirstOrDefault(e => e.IdEjecucion == id);
    }
}
