using System.Collections.Generic;
using System.Threading.Tasks;
using Asistente.Domain.Entities;

namespace Asistente.Domain.Interfaces;

public interface IDocumentoFuenteRepository
{
    Task<IEnumerable<DocumentoFuente>> GetByDocumentoIdAsync(int idDocumento);
    Task<IEnumerable<DocumentoFuente>> GetByFuenteIdAsync(int idFuente);
    Task<DocumentoFuente?> GetByClaveAsync(int idDocumento, int idFuente);
    Task AddAsync(DocumentoFuente documentoFuente);
    void Update(DocumentoFuente documentoFuente);
    void Delete(DocumentoFuente documentoFuente);
    Task<IEnumerable<Documento>> GetDocumentosByFuenteIdAsync(int idFuente);
    Task<IEnumerable<int>> GetDocumentosProcesadosIdsByFuenteAsync(int idFuente);
}
