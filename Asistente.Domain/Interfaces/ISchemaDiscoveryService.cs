using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asistente.Domain.Interfaces;

public class ColumnaEsquemaInfo
{
    public string Nombre { get; set; } = string.Empty;
    public string TipoDato { get; set; } = string.Empty;
    public int? Longitud { get; set; }
    public bool Anulable { get; set; }
    public bool EsClavePrimaria { get; set; }
    public bool EsClaveForanea { get; set; }
}

public class ObjetoEsquemaInfo
{
    public string Esquema { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public List<ColumnaEsquemaInfo> Columnas { get; set; } = new();
}

public class RelacionEsquemaInfo
{
    public string Nombre { get; set; } = string.Empty;
    public string TablaOrigen { get; set; } = string.Empty;
    public string ColumnaOrigen { get; set; } = string.Empty;
    public string TablaDestino { get; set; } = string.Empty;
    public string ColumnaDestino { get; set; } = string.Empty;
}

public class EsquemaBaseDatosInfo
{
    public string BaseDatos { get; set; } = string.Empty;
    public List<ObjetoEsquemaInfo> Tablas { get; set; } = new();
    public List<ObjetoEsquemaInfo> Vistas { get; set; } = new();
    public List<RelacionEsquemaInfo> Relaciones { get; set; } = new();
}

public interface ISchemaDiscoveryService
{
    Task<EsquemaBaseDatosInfo> DiscoverAsync(string connectionString, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string connectionString, CancellationToken cancellationToken = default);
}
