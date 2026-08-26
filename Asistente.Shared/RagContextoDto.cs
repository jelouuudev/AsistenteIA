using System;
using System.Collections.Generic;

namespace Asistente.Shared;

public class RagContextoDto
{
    public string ContextoDocumental { get; set; } = string.Empty;
    public List<FragmentoRelevanteDto> FragmentosRecuperados { get; set; } = new();
    public int TotalFragmentos { get; set; }
    public int TokensEstimadosContexto { get; set; }
    public long TiempoRecuperacionMs { get; set; }
    public List<ReferenciaDocumentalDto> Referencias { get; set; } = new();
}
