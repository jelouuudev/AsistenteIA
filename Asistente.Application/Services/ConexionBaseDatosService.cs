using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asistente.Application.Interfaces;
using Asistente.Domain.Entities;
using Asistente.Domain.Interfaces;
using Asistente.Shared;
using Microsoft.Extensions.Logging;

namespace Asistente.Application.Services;

public class ConexionBaseDatosService : IConexionBaseDatosService
{
    private readonly IConexionBaseDatosRepository _conexionRepository;
    private readonly ITablaAutorizadaRepository _tablaRepository;
    private readonly IVistaAutorizadaRepository _vistaRepository;
    private readonly IConsultaEjecutadaRepository _consultaRepository;
    private readonly IConexionCifrador _cifrador;
    private readonly ISchemaDiscoveryService _schemaDiscovery;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ConexionBaseDatosService> _logger;

    public ConexionBaseDatosService(
        IConexionBaseDatosRepository conexionRepository,
        ITablaAutorizadaRepository tablaRepository,
        IVistaAutorizadaRepository vistaRepository,
        IConsultaEjecutadaRepository consultaRepository,
        IConexionCifrador cifrador,
        ISchemaDiscoveryService schemaDiscovery,
        IUnitOfWork unitOfWork,
        ILogger<ConexionBaseDatosService> logger)
    {
        _conexionRepository = conexionRepository;
        _tablaRepository = tablaRepository;
        _vistaRepository = vistaRepository;
        _consultaRepository = consultaRepository;
        _cifrador = cifrador;
        _schemaDiscovery = schemaDiscovery;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IEnumerable<ConexionBaseDatosDto>> ObtenerTodasAsync(CancellationToken cancellationToken = default)
    {
        var conexiones = (await _conexionRepository.GetAllAsync()).ToList();
        var resultado = new List<ConexionBaseDatosDto>();

        foreach (var c in conexiones)
        {
            var tablas = (await _tablaRepository.GetByConexionIdAsync(c.IdConexion)).ToList();
            var vistas = (await _vistaRepository.GetByConexionIdAsync(c.IdConexion)).ToList();

            resultado.Add(new ConexionBaseDatosDto
            {
                IdConexion = c.IdConexion,
                Nombre = c.Nombre,
                Servidor = c.Servidor,
                BaseDatos = c.BaseDatos,
                UsuarioConexion = c.UsuarioConexion,
                Activa = c.Activa,
                FechaRegistro = c.FechaRegistro,
                TotalTablasAutorizadas = tablas.Count,
                TotalVistasAutorizadas = vistas.Count,
                TotalConsultasEjecutadas = c.ConsultasEjecutadas?.Count ?? 0
            });
        }

        return resultado;
    }

    public async Task<IEnumerable<ConexionBaseDatosDto>> ObtenerActivasAsync(CancellationToken cancellationToken = default)
    {
        var activas = (await _conexionRepository.GetActivasAsync()).ToList();
        return activas.Select(c => new ConexionBaseDatosDto
        {
            IdConexion = c.IdConexion,
            Nombre = c.Nombre,
            Servidor = c.Servidor,
            BaseDatos = c.BaseDatos,
            UsuarioConexion = c.UsuarioConexion,
            Activa = c.Activa,
            FechaRegistro = c.FechaRegistro
        });
    }

    public async Task<ConexionBaseDatosDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var c = await _conexionRepository.GetByIdAsync(id);
        if (c == null) return null;

        return new ConexionBaseDatosDto
        {
            IdConexion = c.IdConexion,
            Nombre = c.Nombre,
            Servidor = c.Servidor,
            BaseDatos = c.BaseDatos,
            UsuarioConexion = c.UsuarioConexion,
            Activa = c.Activa,
            FechaRegistro = c.FechaRegistro,
            TotalTablasAutorizadas = c.TablasAutorizadas?.Count ?? 0,
            TotalVistasAutorizadas = c.VistasAutorizadas?.Count ?? 0,
            TotalConsultasEjecutadas = c.ConsultasEjecutadas?.Count ?? 0
        };
    }

    public async Task<ConexionBaseDatosDto> CrearAsync(CrearConexionBaseDatosRequest request, CancellationToken cancellationToken = default)
    {
        var conexion = new ConexionBaseDatos
        {
            Nombre = request.Nombre,
            Servidor = request.Servidor,
            BaseDatos = request.BaseDatos,
            UsuarioConexion = request.AutenticacionWindows ? string.Empty : request.UsuarioConexion,
            CadenaConexionCifrada = _cifrador.Cifrar(ConstruirCadenaConexion(request)),
            Activa = true,
            FechaRegistro = DateTime.UtcNow
        };

        await _conexionRepository.AddAsync(conexion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Conexión a base de datos '{Nombre}' registrada con ID {Id}.",
            conexion.Nombre, conexion.IdConexion);

        return (await ObtenerPorIdAsync(conexion.IdConexion, cancellationToken))!;
    }

    public async Task<ConexionBaseDatosDto> ActualizarAsync(int id, ActualizarConexionBaseDatosRequest request, CancellationToken cancellationToken = default)
    {
        var conexion = await _conexionRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Conexión con ID {id} no encontrada.");

        conexion.Nombre = request.Nombre;
        conexion.Servidor = request.Servidor;
        conexion.BaseDatos = request.BaseDatos;
        conexion.UsuarioConexion = request.AutenticacionWindows ? "" : request.UsuarioConexion;
        conexion.Activa = request.Activa;

        if (!request.AutenticacionWindows || !string.IsNullOrWhiteSpace(request.Contrasena))
        {
            conexion.CadenaConexionCifrada = _cifrador.Cifrar(ConstruirCadenaConexion(new CrearConexionBaseDatosRequest
            {
                Nombre = request.Nombre,
                Servidor = request.Servidor,
                BaseDatos = request.BaseDatos,
                UsuarioConexion = request.UsuarioConexion,
                Contrasena = request.Contrasena,
                AutenticacionWindows = request.AutenticacionWindows
            }));
        }

        _conexionRepository.Update(conexion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Conexión '{Nombre}' actualizada.", conexion.Nombre);
        return (await ObtenerPorIdAsync(id, cancellationToken))!;
    }

    public async Task ActivarAsync(int id, CancellationToken cancellationToken = default)
    {
        var conexion = await _conexionRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Conexión con ID {id} no encontrada.");

        conexion.Activa = true;
        _conexionRepository.Update(conexion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DesactivarAsync(int id, CancellationToken cancellationToken = default)
    {
        var conexion = await _conexionRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Conexión con ID {id} no encontrada.");

        conexion.Activa = false;
        _conexionRepository.Update(conexion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        var conexion = await _conexionRepository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Conexión con ID {id} no encontrada.");

        var consultas = (await _consultaRepository.GetByConexionIdAsync(id)).ToList();
        if (consultas.Count > 0)
        {
            throw new InvalidOperationException(
                $"No se puede eliminar la conexión '{conexion.Nombre}' porque tiene {consultas.Count} consulta(s) registradas en la auditoría. Desactívela en su lugar para conservar el historial.");
        }

        _conexionRepository.Delete(conexion);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Conexión {Id} eliminada.", id);
    }

    public async Task<ConexionPruebaResultadoDto> ProbarConexionAsync(ProbarConexionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var cadena = ConstruirCadenaConexion(new CrearConexionBaseDatosRequest
            {
                Servidor = request.Servidor,
                BaseDatos = request.BaseDatos,
                UsuarioConexion = request.UsuarioConexion ?? string.Empty,
                Contrasena = request.Contrasena,
                AutenticacionWindows = request.AutenticacionWindows
            });

            var exitoso = await _schemaDiscovery.TestConnectionAsync(cadena, cancellationToken);
            return new ConexionPruebaResultadoDto
            {
                Exitoso = exitoso,
                Mensaje = exitoso
                    ? "Conexión exitosa al servidor de base de datos."
                    : "No se pudo establecer la conexión. Verifique los datos proporcionados."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error probando conexión a {Servidor}/{BaseDatos}.", request.Servidor, request.BaseDatos);
            return new ConexionPruebaResultadoDto
            {
                Exitoso = false,
                Mensaje = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<EsquemaBaseDatosDto> DescubrirEsquemaAsync(int idConexion, CancellationToken cancellationToken = default)
    {
        var conexion = await _conexionRepository.GetByIdAsync(idConexion)
            ?? throw new KeyNotFoundException($"Conexión con ID {idConexion} no encontrada.");

        var cadena = _cifrador.Descifrar(conexion.CadenaConexionCifrada);
        var esquema = await _schemaDiscovery.DiscoverAsync(cadena, cancellationToken);

        return new EsquemaBaseDatosDto
        {
            BaseDatos = esquema.BaseDatos,
            Tablas = esquema.Tablas.Select(t => new EsquemaObjetoDto
            {
                Esquema = t.Esquema,
                Nombre = t.Nombre,
                Tipo = t.Tipo,
                Columnas = t.Columnas.Select(c => new ColumnaEsquemaDto
                {
                    Nombre = c.Nombre,
                    TipoDato = c.TipoDato,
                    Longitud = c.Longitud,
                    Anulable = c.Anulable,
                    EsClavePrimaria = c.EsClavePrimaria,
                    EsClaveForanea = c.EsClaveForanea
                }).ToList()
            }).ToList(),
            Vistas = esquema.Vistas.Select(v => new EsquemaObjetoDto
            {
                Esquema = v.Esquema,
                Nombre = v.Nombre,
                Tipo = v.Tipo,
                Columnas = v.Columnas.Select(c => new ColumnaEsquemaDto
                {
                    Nombre = c.Nombre,
                    TipoDato = c.TipoDato,
                    Longitud = c.Longitud,
                    Anulable = c.Anulable,
                    EsClavePrimaria = c.EsClavePrimaria,
                    EsClaveForanea = c.EsClaveForanea
                }).ToList()
            }).ToList(),
            Relaciones = esquema.Relaciones.Select(r => new RelacionEsquemaDto
            {
                Nombre = r.Nombre,
                TablaOrigen = r.TablaOrigen,
                ColumnaOrigen = r.ColumnaOrigen,
                TablaDestino = r.TablaDestino,
                ColumnaDestino = r.ColumnaDestino
            }).ToList()
        };
    }

    public async Task<IEnumerable<TablaAutorizadaDto>> ObtenerTablasAutorizadasAsync(int idConexion, CancellationToken cancellationToken = default)
    {
        var tablas = await _tablaRepository.GetByConexionIdAsync(idConexion);
        return tablas.Select(t => new TablaAutorizadaDto
        {
            IdTabla = t.IdTabla,
            IdConexion = t.IdConexion,
            NombreTabla = t.NombreTabla,
            Esquema = t.Esquema,
            Descripcion = t.Descripcion,
            Activa = t.Activa
        });
    }

    public async Task<IEnumerable<VistaAutorizadaDto>> ObtenerVistasAutorizadasAsync(int idConexion, CancellationToken cancellationToken = default)
    {
        var vistas = await _vistaRepository.GetByConexionIdAsync(idConexion);
        return vistas.Select(v => new VistaAutorizadaDto
        {
            IdVista = v.IdVista,
            IdConexion = v.IdConexion,
            NombreVista = v.NombreVista,
            Descripcion = v.Descripcion,
            Activa = v.Activa
        });
    }

    public async Task AgregarTablaAutorizadaAsync(int idConexion, TablaAutorizadaRequest request, CancellationToken cancellationToken = default)
    {
        var conexion = await _conexionRepository.GetByIdAsync(idConexion)
            ?? throw new KeyNotFoundException($"Conexión con ID {idConexion} no encontrada.");

        var tabla = new TablaAutorizada
        {
            IdConexion = idConexion,
            NombreTabla = request.NombreTabla,
            Esquema = string.IsNullOrWhiteSpace(request.Esquema) ? "dbo" : request.Esquema,
            Descripcion = request.Descripcion,
            Activa = true
        };

        await _tablaRepository.AddAsync(tabla);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tabla {Esquema}.{Tabla} autorizada en conexión {IdConexion}.",
            tabla.Esquema, tabla.NombreTabla, idConexion);
    }

    public async Task AgregarVistaAutorizadaAsync(int idConexion, VistaAutorizadaRequest request, CancellationToken cancellationToken = default)
    {
        var conexion = await _conexionRepository.GetByIdAsync(idConexion)
            ?? throw new KeyNotFoundException($"Conexión con ID {idConexion} no encontrada.");

        var vista = new VistaAutorizada
        {
            IdConexion = idConexion,
            NombreVista = request.NombreVista,
            Descripcion = request.Descripcion,
            Activa = true
        };

        await _vistaRepository.AddAsync(vista);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Vista {Vista} autorizada en conexión {IdConexion}.",
            vista.NombreVista, idConexion);
    }

    public async Task EliminarTablaAutorizadaAsync(int idConexion, int idTabla, CancellationToken cancellationToken = default)
    {
        var tabla = await _tablaRepository.GetByIdAsync(idTabla);
        if (tabla != null && tabla.IdConexion == idConexion)
        {
            _tablaRepository.Delete(tabla);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task EliminarVistaAutorizadaAsync(int idConexion, int idVista, CancellationToken cancellationToken = default)
    {
        var vista = await _vistaRepository.GetByIdAsync(idVista);
        if (vista != null && vista.IdConexion == idConexion)
        {
            _vistaRepository.Delete(vista);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<string> ConstruirCadenaConexionAsync(CrearConexionBaseDatosRequest request)
    {
        return Task.FromResult(ConstruirCadenaConexion(request));
    }

    private static string ConstruirCadenaConexion(CrearConexionBaseDatosRequest request)
    {
        if (request.AutenticacionWindows)
        {
            return $"Server={request.Servidor};Database={request.BaseDatos};Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;";
        }

        return $"Server={request.Servidor};Database={request.BaseDatos};User Id={request.UsuarioConexion};Password={request.Contrasena};TrustServerCertificate=True;Encrypt=False;Integrated Security=false;";
    }
}
