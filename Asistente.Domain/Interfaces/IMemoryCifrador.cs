namespace Asistente.Domain.Interfaces;

/// <summary>
/// Cifrado del contenido de las memorias empresariales (ETAPA 21, sección 16).
/// Misma forma que <see cref="ICredencialCifrador"/> e <see cref="IConexionCifrador"/>.
/// </summary>
public interface IMemoryCifrador
{
    string Cifrar(string textoPlano);
    string Descifrar(string textoGuardado);
}
