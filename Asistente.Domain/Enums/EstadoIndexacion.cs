namespace Asistente.Domain.Enums;

public enum EstadoIndexacion
{
    Pendiente = 0,
    EnProceso = 1,
    Indexado = 2,
    Error = 3,
    /// <summary>
    /// Versión no vigente (inactiva): sin vectores por diseño. El servicio de fondo
    /// lo ignora (no reintenta), evitando resucitar vectores de versiones viejas.
    /// </summary>
    Excluido = 4
}
