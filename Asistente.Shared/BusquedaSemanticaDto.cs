using System;
using System.Collections.Generic;

namespace Asistente.Shared;

public class BusquedaSemanticaRequest
{
    public string Consulta { get; set; } = string.Empty;
    public int? TopK { get; set; }
}

public class BusquedaSemanticaResponse
{
    public string Consulta { get; set; } = string.Empty;
    public int TotalResultados { get; set; }
    public List<FragmentoRelevanteDto> Fragmentos { get; set; } = new();
    public long TiempoMs { get; set; }
}
