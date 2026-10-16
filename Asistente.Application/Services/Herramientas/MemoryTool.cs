using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Services.Memoria;
using Asistente.Domain.Interfaces;

namespace Asistente.Application.Services.Herramientas;

/// <summary>
/// Herramienta de memoria (ETAPA 21): los pasos Tool del Planner y los agentes
/// consultan y guardan conocimiento institucional vía el Memory Manager.
/// Operaciones: buscar (texto), leer (codigo), guardar (codigo+título+contenido
/// +ámbito). Nunca expone memorias sin permiso: el Manager las filtra.
/// </summary>
public class MemoryTool : ITool
{
    public string Name => "MemoryTool";
    public string Description => "Consulta y guarda memoria institucional (formatos de reportes, definiciones, preferencias, contexto de workflows). Parámetros: operacion (buscar|leer|guardar), texto, codigo, titulo, contenido, ambito (Usuario|Agente|Organizacion|Workflow).";
    public string Categoria => "Memoria";

    private readonly MemoryManager _manager;

    public MemoryTool(MemoryManager manager) => _manager = manager;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default)
    {
        var parametros = request.Parametros ?? new Dictionary<string, object?>();
        string? Valor(string clave) => parametros.TryGetValue(clave, out var v) ? v?.ToString() : null;
        var operacion = (Valor("operacion") ?? "buscar").Trim().ToLowerInvariant();

        try
        {
            if (operacion == "leer")
            {
                var codigo = Valor("codigo");
                if (string.IsNullOrWhiteSpace(codigo))
                    return new ToolExecutionResult { Exitoso = false, Error = "Falta el parámetro 'codigo'." };
                var memoria = await _manager.ObtenerAsync(codigo, request.IdUsuario, request.IdAsistente, cancellationToken);
                return new ToolExecutionResult
                {
                    Exitoso = true,
                    Contenido = $"[{memoria.Ambito}: {memoria.Titulo}]\n{memoria.Contenido}",
                    Metadatos = new Dictionary<string, object?>
                    {
                        ["codigo"] = memoria.Codigo, ["ambito"] = memoria.Ambito,
                        ["version"] = memoria.VersionActual
                    }
                };
            }

            if (operacion == "guardar")
            {
                var codigo = Valor("codigo");
                var contenido = Valor("contenido");
                if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(contenido))
                    return new ToolExecutionResult { Exitoso = false, Error = "Faltan 'codigo' y/o 'contenido'." };
                var memoria = await _manager.CrearAsync(new Shared.CrearMemoryRequest
                {
                    Codigo = codigo,
                    Titulo = Valor("titulo") ?? codigo,
                    Contenido = contenido,
                    Ambito = Valor("ambito") ?? "Usuario",
                    IdPropietario = request.IdUsuario
                }, request.IdUsuario, cancellationToken);
                return new ToolExecutionResult
                {
                    Exitoso = true,
                    Contenido = $"Memoria '{memoria.Codigo}' guardada (v{memoria.VersionActual}).",
                    Metadatos = new Dictionary<string, object?> { ["codigo"] = memoria.Codigo }
                };
            }

            var texto = Valor("texto") ?? Valor("consulta") ?? request.PreguntaOriginal ?? string.Empty;
            var hits = await _manager.BuscarAsync(texto, request.IdUsuario, request.IdAsistente, 5, null, cancellationToken);
            if (hits.Count == 0)
                return new ToolExecutionResult { Exitoso = true, Contenido = "Sin memorias relevantes." };
            return new ToolExecutionResult
            {
                Exitoso = true,
                Contenido = string.Join("\n\n", hits.Select(h => $"[{h.Ambito}: {h.Titulo}]\n{h.Fragmento}")),
                Metadatos = new Dictionary<string, object?> { ["resultados"] = hits.Count }
            };
        }
        catch (System.UnauthorizedAccessException ex)
        {
            return new ToolExecutionResult { Exitoso = false, Error = ex.Message };
        }
        catch (System.Collections.Generic.KeyNotFoundException ex)
        {
            return new ToolExecutionResult { Exitoso = false, Error = ex.Message };
        }
    }
}
