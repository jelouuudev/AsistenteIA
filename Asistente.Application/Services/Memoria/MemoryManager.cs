using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services.Memoria;

/// <summary>
/// Memory Manager (ETAPA 21): CRUD de memorias con versionado append-only,
/// permisos (MEMORIA:{Codigo} o propiedad en ámbito Usuario), expiración perezosa
/// y auditoría total. La indexación semántica es best-effort: jamás tumba la
/// operación. Eliminar respeta la política (trazabilidad o purga).
///
/// El historial de versiones no lo gestiona aquí sino el Memory Version Manager:
/// esta clase compone contenido y versión y se la pasa; el control de acceso a
/// una versión sigue siendo de esta capa, que es quien conoce al usuario.
/// </summary>
public class MemoryManager
{
    private readonly IMemoryRepository _repo;
    private readonly MemoryVersionManager _versiones;
    private readonly IMemoryAccessLogRepository _logs;
    private readonly SemanticMemorySearch _busqueda;
    private readonly MemoryRetentionEngine _retencion;
    private readonly IAutorizacionService _autorizacion;
    private readonly ILogger<MemoryManager> _logger;

    public MemoryManager(
        IMemoryRepository repo,
        MemoryVersionManager versiones,
        IMemoryAccessLogRepository logs,
        SemanticMemorySearch busqueda,
        MemoryRetentionEngine retencion,
        IAutorizacionService autorizacion,
        ILogger<MemoryManager> logger)
    {
        _repo = repo;
        _versiones = versiones;
        _logs = logs;
        _busqueda = busqueda;
        _retencion = retencion;
        _autorizacion = autorizacion;
        _logger = logger;
    }

    public async Task<MemoryDto> CrearAsync(CrearMemoryRequest request, int idUsuario, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            throw new ArgumentException("El código de la memoria es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Contenido))
            throw new ArgumentException("El contenido es obligatorio.");
        if (await _repo.GetByCodigoAsync(request.Codigo.Trim(), ct) != null)
            throw new InvalidOperationException($"Ya existe la memoria '{request.Codigo}'.");

        var scopes = await _repo.GetScopesAsync(ct);
        var scope = scopes.FirstOrDefault(s =>
            s.Codigo.Equals(request.Ambito?.Trim() ?? "Organizacion", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Ámbito '{request.Ambito}' inexistente o inactivo.");

        int? idCategoria = null;
        if (!string.IsNullOrWhiteSpace(request.Categoria))
        {
            var cat = (await _repo.GetCategoriesAsync(ct)).FirstOrDefault(c =>
                c.Codigo.Equals(request.Categoria!.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cat == null)
                throw new InvalidOperationException($"Categoría '{request.Categoria}' inexistente.");
            idCategoria = cat.IdCategory;
        }

        // El dueño debe ser coherente con el ámbito: si falta o no existe, la
        // memoria se guarda sin nadie a quien inyectarla (visible en la lista,
        // buscable, y jamás usada). Antes esto pasaba sin aviso: un IdPropietario
        // de 99 en vez de 1 bastaba para dejarla muerta para siempre.
        var ambito = scope.Codigo;
        if (AmbitoConDueno(ambito))
        {
            if (!request.IdPropietario.HasValue)
                throw new InvalidOperationException(
                    $"El ámbito '{ambito}' requiere el Id propietario de su dueño.");
            if (!await _repo.PropietarioExisteAsync(ambito, request.IdPropietario.Value, ct))
                throw new InvalidOperationException(
                    $"El Id propietario {request.IdPropietario.Value} no existe para el ámbito '{ambito}'.");
        }
        else if (request.IdPropietario.HasValue)
        {
            throw new InvalidOperationException(
                $"El ámbito '{ambito}' no admite Id propietario: déjalo vacío.");
        }

        var ahora = DateTime.UtcNow;
        var memoria = new MemoryRecord
        {
            Codigo = request.Codigo.Trim(),
            Titulo = string.IsNullOrWhiteSpace(request.Titulo) ? request.Codigo.Trim() : request.Titulo.Trim(),
            Contenido = request.Contenido,
            IdScope = scope.IdScope,
            IdPropietario = request.IdPropietario,
            IdCategory = idCategoria,
            Activo = true,
            VersionActual = 1,
            // Sin especificar se protege por defecto: una memoria compartida no
            // debe quedar abierta por olvido de quien la crea.
            RequierePermiso = request.RequierePermiso ?? true,
            FechaExpiracion = request.FechaExpiracion,
            FechaCreacion = ahora,
            FechaActualizacion = ahora
        };
        if (request.DiasRetencion.HasValue && request.DiasRetencion.Value > 0 && !memoria.FechaExpiracion.HasValue)
            memoria.FechaExpiracion = ahora.AddDays(request.DiasRetencion.Value);

        // Regla de negocio: la memoria de un ámbito compartido NO se crea sin
        // autorización. Antes este método era el único que no comprobaba permiso:
        // cualquier usuario autenticado podía escribir en la memoria
        // organizacional (verificado en vivo con los roles Administrador y
        // Operador). El ámbito Usuario propio queda exento, igual que en la
        // lectura: cada usuario mantiene sus propias preferencias sin trámite.
        if (!await AutorizadoEscribirAsync(scope.Codigo, memoria.IdPropietario, idUsuario, ct))
        {
            await AuditarAsync(memoria, idUsuario, null, "Crear", "Denegado",
                $"Sin permiso {PermisoAdministrarMemoria} para escribir en el ámbito '{scope.Codigo}'.", ct);
            throw new UnauthorizedAccessException(
                $"Sin permiso {PermisoAdministrarMemoria} para crear memorias en el ámbito '{scope.Codigo}'.");
        }

        await _repo.AddAsync(memoria, ct);
        await _versiones.RegistrarAsync(memoria, memoria.Contenido, "Creación", idUsuario, ct);
        await _busqueda.IndexarAsync(memoria, ct);
        if (memoria.Indexada)
            await _repo.UpdateAsync(memoria, ct);
        await AuditarAsync(memoria, idUsuario, null, "Crear", "Exitoso", null, ct);
        return ADto(memoria, scope.Codigo);
    }

    public async Task<MemoryDto> ObtenerAsync(string codigo, int idUsuario, int? idAsistente = null, CancellationToken ct = default)
    {
        var memoria = await _repo.GetByCodigoAsync(codigo, ct);
        if (memoria == null || !memoria.Activo)
        {
            await AuditarCodigoAsync(codigo, null, idUsuario, idAsistente, "Leer", "NoEncontrada", null, ct);
            throw new KeyNotFoundException($"No existe la memoria '{codigo}'.");
        }
        var ambito = memoria.Scope?.Codigo ?? string.Empty;
        if (await ExpiradaAsync(memoria, ct))
        {
            await AuditarAsync(memoria, idUsuario, idAsistente, "Leer", "Expirada", null, ct);
            throw new KeyNotFoundException($"La memoria '{codigo}' expiró.");
        }
        if (!await AutorizadoAsync(memoria, ambito, idUsuario, ct))
        {
            await AuditarAsync(memoria, idUsuario, idAsistente, "Leer", "Denegado",
                $"Sin permiso MEMORIA:{memoria.Codigo}.", ct);
            throw new UnauthorizedAccessException($"Sin permiso MEMORIA:{memoria.Codigo}.");
        }
        await AuditarAsync(memoria, idUsuario, idAsistente, "Leer", "Exitoso", null, ct);
        return ADto(memoria, ambito);
    }

    public async Task<MemoryDto> ActualizarAsync(string codigo, ActualizarMemoryRequest request,
        int idUsuario, CancellationToken ct = default, bool ignorarExpiracion = false)
    {
        var memoria = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe la memoria '{codigo}'.");
        if (!memoria.Activo)
            throw new InvalidOperationException($"La memoria '{codigo}' está inactiva.");
        var ambito = memoria.Scope?.Codigo ?? string.Empty;
        // ignorarExpiracion lo usa solo el estado de un workflow: se reescribe en cada
        // ejecución, así que un registro que caducó debe poder renovarse en vez de
        // quedar con el contenido viejo congelado para siempre.
        if (!ignorarExpiracion && await ExpiradaAsync(memoria, ct))
            throw new InvalidOperationException($"La memoria '{codigo}' expiró.");
        if (!await AutorizadoAsync(memoria, ambito, idUsuario, ct))
        {
            await AuditarAsync(memoria, idUsuario, null, "Actualizar", "Denegado",
                $"Sin permiso MEMORIA:{memoria.Codigo}.", ct);
            throw new UnauthorizedAccessException($"Sin permiso para modificar '{codigo}'.");
        }
        if (string.IsNullOrWhiteSpace(request.Contenido))
            throw new ArgumentException("El contenido es obligatorio.");

        var ahora = DateTime.UtcNow;
        memoria.VersionActual++;
        memoria.Contenido = request.Contenido;
        memoria.FechaActualizacion = ahora;
        await _repo.UpdateAsync(memoria, ct);
        await _versiones.RegistrarAsync(memoria, memoria.Contenido, request.Motivo, idUsuario, ct);
        await _busqueda.IndexarAsync(memoria, ct);
        if (memoria.Indexada)
            await _repo.UpdateAsync(memoria, ct);
        await AuditarAsync(memoria, idUsuario, null, "Actualizar", "Exitoso",
            $"Versión {memoria.VersionActual}.", ct);
        return ADto(memoria, ambito);
    }

    public async Task<MemoryDto> RevertirAsync(string codigo, int numero, string? motivo,
        int idUsuario, CancellationToken ct = default)
    {
        // Revertir NO borra ni reescribe: anexa el contenido de la versión elegida
        // como versión nueva (append-only). El control de acceso y la expiración los
        // verifica ActualizarAsync, por la misma vía que cualquier cambio.
        var idMemory = (await _repo.GetByCodigoAsync(codigo, ct))?.IdMemory ?? 0;
        var version = await _versiones.ObtenerAsync(idMemory, numero, ct)
            ?? throw new KeyNotFoundException($"La memoria '{codigo}' no tiene versión {numero}.");
        return await ActualizarAsync(codigo, new ActualizarMemoryRequest
        {
            Contenido = version.Contenido,
            Motivo = $"Reversión a v{numero}. {motivo}".Trim()
        }, idUsuario, ct);
    }

    public async Task EliminarAsync(string codigo, int idUsuario, CancellationToken ct = default)
    {
        var memoria = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe la memoria '{codigo}'.");
        var ambito = memoria.Scope?.Codigo ?? string.Empty;
        if (!await AutorizadoAsync(memoria, ambito, idUsuario, ct))
        {
            await AuditarAsync(memoria, idUsuario, null, "Eliminar", "Denegado",
                $"Sin permiso MEMORIA:{memoria.Codigo}.", ct);
            throw new UnauthorizedAccessException($"Sin permiso para eliminar '{codigo}'.");
        }
        var politicas = await _repo.GetPoliticasAsync(ct);
        var conservar = politicas
            .FirstOrDefault(p => p.IdScope == memoria.IdScope && p.Activo)?.ConservarTrazabilidad
            ?? politicas.FirstOrDefault(p => p.IdScope == null && p.Activo)?.ConservarTrazabilidad
            ?? true;
        if (conservar)
        {
            memoria.Activo = false;
            memoria.FechaActualizacion = DateTime.UtcNow;
            await _repo.UpdateAsync(memoria, ct);
        }
        else
        {
            await _busqueda.DesindexarAsync(memoria, ct);
            await _repo.DeleteAsync(memoria, ct);
        }
        await AuditarAsync(memoria, idUsuario, null, "Eliminar", "Exitoso",
            conservar ? "Desactivada (trazabilidad)." : "Purgada.", ct);
    }

    public async Task<List<MemorySearchHitDto>> BuscarAsync(string texto, int idUsuario,
        int? idAsistente = null, int topK = 5, string? ambito = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(texto)) return new List<MemorySearchHitDto>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var candidatas = await _repo.GetActivasAsync(ct);
        if (!string.IsNullOrWhiteSpace(ambito))
            candidatas = candidatas.Where(m =>
                (m.Scope?.Codigo ?? string.Empty).Equals(ambito.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        var ahora = DateTime.UtcNow;
        var politicas = await _repo.GetPoliticasAsync(ct);
        var guias = (await _busqueda.BuscarIdsAsync(texto, topK, ct))
            .ToDictionary(x => x.VectorId, x => (x.Texto, x.Puntaje));
        var orden = new List<(MemoryRecord M, double Puntaje, string Fragmento)>();

        foreach (var memoria in candidatas)
        {
            if (!await AutorizadoAsync(memoria, memoria.Scope?.Codigo ?? string.Empty, idUsuario, ct))
                continue;
            var dias = politicas.FirstOrDefault(p => p.IdScope == memoria.IdScope && p.Activo)?.DiasRetencion
                ?? politicas.FirstOrDefault(p => p.IdScope == null && p.Activo)?.DiasRetencion;
            if (MemoryRetentionEngine.VencimientoEfectivo(memoria, dias) is DateTime vence && vence <= ahora)
                continue;
            double puntaje = 0;
            string fragmento = memoria.Contenido.Length > 220 ? memoria.Contenido[..220] + "…" : memoria.Contenido;
            if (memoria.VectorId.HasValue && guias.TryGetValue(memoria.VectorId.Value, out var g))
            {
                puntaje = g.Puntaje;
                fragmento = g.Texto.Length > 220 ? g.Texto[..220] + "…" : g.Texto;
            }
            else
            {
                // Sin índice (vector caído o pendiente): coincidencia textual como
                // respaldo para no devolver vacío. Puntaje base bajo.
                if (!ContieneTexto(memoria, texto)) continue;
                puntaje = 0.01;
            }
            orden.Add((memoria, puntaje, fragmento));
        }

        var hits = orden.OrderByDescending(x => x.Puntaje).Take(Math.Max(topK, 1))
            .Select(x => new MemorySearchHitDto
            {
                Codigo = x.M.Codigo,
                Titulo = x.M.Titulo,
                Fragmento = x.Fragmento,
                Ambito = x.M.Scope?.Codigo ?? string.Empty,
                Puntaje = x.Puntaje
            }).ToList();
        await _logs.AddAsync(new MemoryAccessLog
        {
            Codigo = $"BUSQUEDA:{(texto.Length > 60 ? texto[..60] : texto)}",
            IdUsuario = idUsuario,
            IdAsistente = idAsistente,
            Operacion = "Buscar",
            Resultado = "Exitoso",
            Motivo = $"{hits.Count} resultado(s).",
            // Tiempo de recuperación: desde que entra la consulta hasta que se
            // resuelven los hits. Es la metrica que pide la observabilidad.
            DuracionMs = (int)sw.ElapsedMilliseconds,
            Fecha = ahora
        }, ct);
        return hits;
    }

    /// <summary>
    /// Audita la inyección de una memoria en el prompt de un agente.
    ///
    /// Es la lectura que ocurre en CADA mensaje y antes no dejaba rastro:
    /// ObtenerContextoAsync compone el contexto con ListarAsync, que no audita
    /// nada. Sin esto, el uso real de la memoria en el runtime era invisible y el
    /// campo IdAsistente de la auditoría quedaba siempre en null (verificado
    /// contra la tabla: 32 accesos, cero con agente).
    ///
    /// Se audita una entrada por memoria efectivamente incluida, no una por
    /// mensaje: el bloque está acotado por caracteres (1500 en el chat), así que
    /// son unas pocas filas y el tipo de memoria queda registrado por separado.
    /// Nunca propaga excepción: auditar nunca tumba una respuesta.
    /// </summary>
    public async Task AuditarInyeccionAsync(MemoryDto memoria, int idUsuario, int? idAsistente,
        int duracionMs, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(memoria.Codigo)) return;
        try
        {
            await _logs.AddAsync(new MemoryAccessLog
            {
                IdMemory = memoria.IdMemory,
                Codigo = memoria.Codigo,
                Ambito = memoria.Ambito,
                IdUsuario = idUsuario == 0 ? null : idUsuario,
                IdAsistente = idAsistente,
                Operacion = "Inyectar",
                Resultado = "Exitoso",
                Motivo = $"Inyectada en el prompt del agente {(idAsistente.HasValue ? idAsistente.Value.ToString() : "(sin agente)")}.",
                DuracionMs = duracionMs,
                Fecha = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memoria: no se pudo auditar la inyección de '{Codigo}'.", memoria.Codigo);
        }
    }

    public async Task<List<MemoryAccessLogDto>> AuditoriaAsync(string codigo, int idUsuario,
        int tope = 100, CancellationToken ct = default)
    {
        var memoria = await _repo.GetByCodigoAsync(codigo, ct);
        // Sin la memoria solo quedarían sus eventos de purga (IdMemory null);
        // se devuelve vacío en vez de exponer barridos caros.
        if (memoria == null) return new List<MemoryAccessLogDto>();
        if (!await AutorizadoAsync(memoria, memoria.Scope?.Codigo ?? string.Empty, idUsuario, ct))
            throw new UnauthorizedAccessException($"Sin permiso para '{codigo}'.");
return (await _logs.GetByMemoryAsync(memoria.IdMemory, tope, ct))
              .Select(Mapear).ToList();
      }

      /// <summary>
      /// Auditoría reciente de todas las memorias, para el panel de memoria.
      ///
      /// No filtra por memoria ni exige permiso por código: a quien llega aquí ya se
      /// le pidió AUDITORIA_CONSULTAR en el endpoint. Exigir además MEMORIA:{codigo}
      /// por cada fila dejaría el panel en blanco salvo para el dueño de todo.
      /// </summary>
      public async Task<List<MemoryAccessLogDto>> AuditoriaRecienteAsync(int tope = 50,
          CancellationToken ct = default)
          => (await _logs.GetRecientesAsync(tope, ct)).Select(Mapear).ToList();

      /// <summary>Mapeo único del log: lo usan la auditoría de una memoria y la del panel.</summary>
      private static MemoryAccessLogDto Mapear(MemoryAccessLog l) => new()
      {
          Codigo = l.Codigo,
          Ambito = l.Ambito,
          IdUsuario = l.IdUsuario,
          IdAsistente = l.IdAsistente,
          Operacion = l.Operacion,
          Resultado = l.Resultado,
          Motivo = l.Motivo,
          DuracionMs = l.DuracionMs,
          Fecha = l.Fecha
      };

    public async Task<List<MemoryVersionDto>> VersionesAsync(string codigo, int idUsuario, CancellationToken ct = default)
    {
        var memoria = await _repo.GetByCodigoAsync(codigo, ct)
            ?? throw new KeyNotFoundException($"No existe la memoria '{codigo}'.");
        if (!await AutorizadoAsync(memoria, memoria.Scope?.Codigo ?? string.Empty, idUsuario, ct))
            throw new UnauthorizedAccessException($"Sin permiso para '{codigo}'.");
        return await _versiones.ListarDtoAsync(memoria.IdMemory, ct);
    }

    public async Task<List<MemoryDto>> ListarAsync(int idUsuario, string? ambito = null,
        CancellationToken ct = default, int? tope = null)
    {
        var ahora = DateTime.UtcNow;
        var politicas = await _repo.GetPoliticasAsync(ct);
        var salida = new List<MemoryDto>();
        var candidatas = string.IsNullOrWhiteSpace(ambito)
            ? await _repo.GetActivasAsync(ct)
            : (await _repo.GetActivasAsync(ct)).Where(m =>
                (m.Scope?.Codigo ?? string.Empty).Equals(ambito.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var memoria in candidatas)
        {
            if (!await AutorizadoAsync(memoria, memoria.Scope?.Codigo ?? string.Empty, idUsuario, ct))
                continue;
            var dias = politicas.FirstOrDefault(p => p.IdScope == memoria.IdScope && p.Activo)?.DiasRetencion
                ?? politicas.FirstOrDefault(p => p.IdScope == null && p.Activo)?.DiasRetencion;
            if (MemoryRetentionEngine.VencimientoEfectivo(memoria, dias) is DateTime vence && vence <= ahora)
                continue;
            salida.Add(ADto(memoria, memoria.Scope?.Codigo ?? string.Empty));
            // Cada candidata autorizada cuesta una consulta de permisos: con 'tope'
            // el recorrido se corta en cuanto se alcanza lo pedido.
            if (tope.HasValue && salida.Count >= tope.Value) break;
        }
        return salida;
    }

    public async Task<MemoryMetricasDto> MetricasAsync(CancellationToken ct = default)
    {
        var activas = await _repo.GetActivasAsync(ct);
        var ahora = DateTime.UtcNow;
        var politicas = await _repo.GetPoliticasAsync(ct);
        long expiradas = 0;
        var porAmbito = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var memoria in activas)
        {
            var ambito = memoria.Scope?.Codigo ?? "(sin ámbito)";
            var dias = politicas.FirstOrDefault(p => p.IdScope == memoria.IdScope && p.Activo)?.DiasRetencion
                ?? politicas.FirstOrDefault(p => p.IdScope == null && p.Activo)?.DiasRetencion;
            if (MemoryRetentionEngine.VencimientoEfectivo(memoria, dias) is DateTime vence && vence <= ahora)
                expiradas++;
            porAmbito[ambito] = porAmbito.TryGetValue(ambito, out var n) ? n + 1 : 1;
        }
        long versiones = 0;
        foreach (var memoria in activas)
            versiones += await _versiones.ContarAsync(memoria.IdMemory, ct);
        var porAgente = new Dictionary<int, long>();
        foreach (var (agente, total) in await _logs.ContarPorAgenteAsync(ct))
            porAgente[agente ?? 0] = total;

        var porUsuario = new Dictionary<int, long>();
        foreach (var (usuario, total) in await _logs.ContarPorUsuarioAsync(ct))
            porUsuario[usuario ?? 0] = total;

        // Eventos sin ámbito (búsquedas y purgas antiguas) se agrupan aparte en vez
        // de inventar un tipo.
        var accesosPorTipo = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tipo, total) in await _logs.ContarPorTipoAsync(ct))
        {
            var clave = string.IsNullOrWhiteSpace(tipo) ? "(sin tipo)" : tipo!;
            accesosPorTipo[clave] = accesosPorTipo.TryGetValue(clave, out var n) ? n + total : total;
        }

        return new MemoryMetricasDto
        {
            TotalActivas = activas.Count,
            PorAmbito = porAmbito,
            Versiones = versiones,
            Accesos = await _logs.ContarPorOperacionAsync("Leer", ct)
                + await _logs.ContarPorOperacionAsync("Buscar", ct),
            ExpiradasPendientes = expiradas,
            // Lecturas con resultado: una búsqueda puede devolver varias memorias y
            // una misma se relee muchas veces, así que se cuenta por evento de
            // auditoría, que es lo que el panel puede desglosar.
            MemoriasConsultadas = await _logs.ContarPorOperacionAsync("Leer", ct)
                + await _logs.ContarPorOperacionAsync("Buscar", ct),
            TiempoRecuperacionMs = await _logs.DuracionMediaMsAsync("Buscar", ct),
            UsoPorAgente = porAgente,
            UsoPorUsuario = porUsuario,
            AccesosPorTipo = accesosPorTipo
        };
    }

    internal async Task<bool> AutorizadoAsync(MemoryRecord memoria, string ambito, int idUsuario, CancellationToken ct)
    {
        // El ámbito decide quién puede LEER, y no lo salva ni la apertura de la
        // memoria: una nota privada creada sin permiso no deja de ser privada.
        // Antes bastaba crearla abierta para que la viera cualquiera (verificado
        // en vivo: el operador veía la nota privada del administrador y el
        // conocimiento del agente).
        if (!await LegiblePorAsync(memoria, ambito, idUsuario, ct))
            return false;
        if (!memoria.RequierePermiso) return true;
        // El propietario exime el permiso en su propio ámbito Usuario.
        if (ambito.Equals("Usuario", StringComparison.OrdinalIgnoreCase)
            && memoria.IdPropietario == idUsuario)
            return true;
        var permiso = await _autorizacion.VerificarPermisoAsync(
            idUsuario, $"MEMORIA:{memoria.Codigo}", ct);
        return permiso.Permitido;
    }

    /// <summary>
    /// Público de cada ámbito. Usuario y Agente son conocimiento con dueño
    /// concreto: lo ve su dueño, y en el caso de Agente quien puede usar ese
    /// agente (misma regla que el chat). Quien administra la memoria
    /// (MEMORIA_ADMINISTRAR) sí la ve toda, para poder gestionarla.
    /// Workflow, Organizacion y Conversacional no filtran: son compartidos y el
    /// permiso por código sigue cubriéndolos.
    /// </summary>
    internal async Task<bool> LegiblePorAsync(MemoryRecord memoria, string ambito, int idUsuario, CancellationToken ct)
    {
        if (ambito.Equals("Usuario", StringComparison.OrdinalIgnoreCase))
            return memoria.IdPropietario == idUsuario || await AdministraMemoriaAsync(idUsuario, ct);
        if (ambito.Equals("Agente", StringComparison.OrdinalIgnoreCase))
        {
            if (await AdministraMemoriaAsync(idUsuario, ct)) return true;
            // Sin dueño no se inyecta en ningún sitio: no lo enseña nadie.
            if (!memoria.IdPropietario.HasValue) return false;
            var uso = await _autorizacion.VerificarAsistenteAsync(idUsuario, memoria.IdPropietario.Value, ct);
            return uso.Permitido;
        }
        return true;
    }

    private async Task<bool> AdministraMemoriaAsync(int idUsuario, CancellationToken ct)
    {
        var permiso = await _autorizacion.VerificarPermisoAsync(idUsuario, PermisoAdministrarMemoria, ct);
        return permiso.Permitido;
    }

    /// <summary>
    /// Permiso que exige escribir en un ámbito compartido. Es el mismo patrón que
    /// FUENTES_ADMINISTRAR o ASISTENTES_ADMINISTRAR: administeredar el contenido
    /// de la organización es una tarea conprivilegio, no de autoservicio.
    /// </summary>
    public const string PermisoAdministrarMemoria = "MEMORIA_ADMINISTRAR";

    /// <summary>
    /// Ámbitos con dueño concreto: la memoria pertenece a un usuario, a un agente
    /// o a un workflow. En ellos el IdPropietario es obligatorio; en los
    /// compartidos (Organizacion, Conversacional) no debe venir.
    /// </summary>
    internal static bool AmbitoConDueno(string ambito) =>
        ambito.Equals("Usuario", StringComparison.OrdinalIgnoreCase)
        || ambito.Equals("Agente", StringComparison.OrdinalIgnoreCase)
        || ambito.Equals("Workflow", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Autorización de escritura por ámbito. Solo el ámbito Usuario y de su propio
    /// propietario es autoservicio; el resto (Organizacion, Agente, Workflow,
    /// Conversacional) es conocimiento compartido y exige permiso.
    /// </summary>
    internal async Task<bool> AutorizadoEscribirAsync(string ambito, int? idPropietario,
        int idUsuario, CancellationToken ct)
    {
        if (ambito.Equals("Usuario", StringComparison.OrdinalIgnoreCase) && idPropietario == idUsuario)
            return true;
        var permiso = await _autorizacion.VerificarPermisoAsync(
            idUsuario, PermisoAdministrarMemoria, ct);
        return permiso.Permitido;
    }

    internal async Task<bool> ExpiradaAsync(MemoryRecord memoria, CancellationToken ct)
    {
        var politicas = await _repo.GetPoliticasAsync(ct);
        var dias = politicas.FirstOrDefault(p => p.IdScope == memoria.IdScope && p.Activo)?.DiasRetencion
            ?? politicas.FirstOrDefault(p => p.IdScope == null && p.Activo)?.DiasRetencion;
        return MemoryRetentionEngine.VencimientoEfectivo(memoria, dias) is DateTime vence
            && vence <= DateTime.UtcNow;
    }

    private static bool ContieneTexto(MemoryRecord memoria, string texto)
    {
        var agujas = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 4).Select(p => p.ToLowerInvariant()).ToList();
        if (agujas.Count == 0) return true;
        var haystack = ((memoria.Titulo ?? string.Empty) + "\n" + (memoria.Contenido ?? string.Empty)).ToLowerInvariant();
        return agujas.Any(haystack.Contains);
    }

    private async Task AuditarAsync(MemoryRecord memoria, int? idUsuario, int? idAsistente,
        string operacion, string resultado, string? motivo, CancellationToken ct)
    {
        try
        {
            await _logs.AddAsync(new MemoryAccessLog
            {
                IdMemory = memoria.IdMemory,
                Codigo = memoria.Codigo,
                Ambito = memoria.Scope?.Codigo,
                IdUsuario = idUsuario == 0 ? null : idUsuario,
                IdAsistente = idAsistente,
                Operacion = operacion,
                Resultado = resultado,
                Motivo = motivo,
                Fecha = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memoria: no se pudo auditar '{Op}' de '{Codigo}'.", operacion, memoria.Codigo);
        }
    }

    private async Task AuditarCodigoAsync(string codigo, int? idMemory, int? idUsuario, int? idAsistente,
        string operacion, string resultado, string? motivo, CancellationToken ct, string? ambito = null)
    {
        try
        {
            await _logs.AddAsync(new MemoryAccessLog
            {
                IdMemory = idMemory,
                Codigo = codigo,
                // Cuando la memoria no existe no hay ámbito que registrar: se deja
                // null en vez de inventar uno.
                Ambito = ambito,
                IdUsuario = idUsuario == 0 ? null : idUsuario,
                IdAsistente = idAsistente,
                Operacion = operacion,
                Resultado = resultado,
                Motivo = motivo,
                Fecha = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memoria: no se pudo auditar '{Op}' de '{Codigo}'.", operacion, codigo);
        }
    }

    private static MemoryDto ADto(MemoryRecord m, string ambito) => new()
    {
        IdMemory = m.IdMemory,
        Codigo = m.Codigo,
        Titulo = m.Titulo,
        Contenido = m.Contenido,
        Ambito = ambito,
        IdPropietario = m.IdPropietario,
        Categoria = m.Category?.Codigo,
        Activo = m.Activo,
        VersionActual = m.VersionActual,
        RequierePermiso = m.RequierePermiso,
        Indexada = m.Indexada,
        FechaExpiracion = m.FechaExpiracion,
        FechaActualizacion = m.FechaActualizacion
    };
}
