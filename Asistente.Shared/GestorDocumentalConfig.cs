using System.Collections.Generic;

namespace Asistente.Shared;

public class GestorDocumentalConfig
{
    public string RutaDocumentos { get; set; } = @"C:\AsistenteIA_Documentos";
    public int TamanoMaximoMB { get; set; } = 50;
    public List<string> ExtensionesPermitidas { get; set; } = new() { ".pdf" };
}
