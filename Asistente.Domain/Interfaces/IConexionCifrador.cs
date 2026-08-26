namespace Asistente.Domain.Interfaces;

public interface IConexionCifrador
{
    string Cifrar(string textoPlano);
    string Descifrar(string textoCifrado);
}
