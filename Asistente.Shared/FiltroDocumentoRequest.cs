using System;

namespace Asistente.Shared;

public class FiltroDocumentoRequest
{
    public string? Nombre { get; set; }
    public int? IdCategoria { get; set; }
    public string? Estado { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
}
