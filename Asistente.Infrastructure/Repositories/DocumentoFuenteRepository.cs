using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class DocumentoFuenteRepository : IDocumentoFuenteRepository
{
    private readonly AsistenteDbContext _context;

    public DocumentoFuenteRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<DocumentoFuente>> GetByDocumentoIdAsync(int idDocumento)
    {
        return await _context.DocumentosFuentes
            .Include(df => df.Fuente)
            .Where(df => df.IdDocumento == idDocumento)
            .ToListAsync();
    }

    public async Task<IEnumerable<DocumentoFuente>> GetByFuenteIdAsync(int idFuente)
    {
        return await _context.DocumentosFuentes
            .Include(df => df.Documento)
            .Where(df => df.IdFuente == idFuente)
            .ToListAsync();
    }

    public async Task<DocumentoFuente?> GetByClaveAsync(int idDocumento, int idFuente)
    {
        return await _context.DocumentosFuentes
            .Include(df => df.Documento)
            .Include(df => df.Fuente)
            .FirstOrDefaultAsync(df => df.IdDocumento == idDocumento && df.IdFuente == idFuente);
    }

    public async Task AddAsync(DocumentoFuente documentoFuente)
    {
        await _context.DocumentosFuentes.AddAsync(documentoFuente);
    }

    public void Update(DocumentoFuente documentoFuente)
    {
        _context.DocumentosFuentes.Update(documentoFuente);
    }

    public void Delete(DocumentoFuente documentoFuente)
    {
        _context.DocumentosFuentes.Remove(documentoFuente);
    }

    public async Task<IEnumerable<Documento>> GetDocumentosByFuenteIdAsync(int idFuente)
    {
        return await _context.DocumentosFuentes
            .Where(df => df.IdFuente == idFuente && df.Activo)
            .Select(df => df.Documento!)
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetDocumentosProcesadosIdsByFuenteAsync(int idFuente)
    {
        // SEGURIDAD: solo documentos en estado Activo. Archivados, borradores y
        // eliminados no deben aparecer en recuperacion aunque sigan asignados.
        var documentoIds = await _context.DocumentosFuentes
            .Where(df => df.IdFuente == idFuente && df.Activo
                && df.Documento != null && df.Documento.Estado == EstadoDocumento.Activo)
            .Select(df => df.IdDocumento)
            .ToListAsync();

        var versionesIds = await _context.DocumentoVersiones
            .Where(v => documentoIds.Contains(v.IdDocumento) && v.Activo)
            .Select(v => v.IdVersion)
            .ToListAsync();

        var procesadosIds = await _context.DocumentosProcesados
            .Where(dp => versionesIds.Contains(dp.IdVersionDocumento))
            .Select(dp => dp.IdDocumentoProcesado)
            .ToListAsync();

        return procesadosIds;
    }
}
