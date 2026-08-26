using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Asistente.Domain.Entities;
using Asistente.Domain.Enums;
using Asistente.Domain.Interfaces;
using Asistente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Infrastructure.Repositories;

public class ProcesamientoDocumentalRepository : IProcesamientoDocumentalRepository
{
    private readonly AsistenteDbContext _context;

    public ProcesamientoDocumentalRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<DocumentoProcesado?> GetByIdAsync(int id)
    {
        return await _context.DocumentosProcesados
            .Include(p => p.VersionDocumento)
            .ThenInclude(v => v!.Documento)
            .FirstOrDefaultAsync(p => p.IdDocumentoProcesado == id);
    }

    public async Task<DocumentoProcesado?> GetByIdWithChunksAsync(int id)
    {
        return await _context.DocumentosProcesados
            .Include(p => p.VersionDocumento)
            .ThenInclude(v => v!.Documento)
            .Include(p => p.Chunks)
            .OrderByDescending(p => p.FechaInicio)
            .FirstOrDefaultAsync(p => p.IdDocumentoProcesado == id);
    }

    public async Task<DocumentoProcesado?> GetByVersionIdAsync(int versionId)
    {
        return await _context.DocumentosProcesados
            .Include(p => p.VersionDocumento)
            .Include(p => p.Chunks)
            .Where(p => p.IdVersionDocumento == versionId)
            .OrderByDescending(p => p.FechaInicio)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<DocumentoProcesado>> GetAllAsync()
    {
        return await _context.DocumentosProcesados
            .Include(p => p.VersionDocumento)
            .ThenInclude(v => v!.Documento)
            .Include(p => p.Chunks)
            .OrderByDescending(p => p.FechaInicio)
            .ToListAsync();
    }

    public async Task<IEnumerable<DocumentoProcesado>> GetByEstadoAsync(EstadoProcesamiento estado)
    {
        return await _context.DocumentosProcesados
            .Include(p => p.VersionDocumento)
            .ThenInclude(v => v!.Documento)
            .Where(p => p.Estado == estado)
            .OrderByDescending(p => p.FechaInicio)
            .ToListAsync();
    }

    public async Task<IEnumerable<DocumentoProcesado>> GetPendingDocumentsForProcessingAsync(int maxDocuments)
    {
        return await _context.DocumentosProcesados
            .Include(p => p.VersionDocumento)
            .ThenInclude(v => v!.Documento)
            .Where(p => p.Estado == EstadoProcesamiento.Pendiente)
            .OrderBy(p => p.FechaInicio)
            .Take(maxDocuments)
            .ToListAsync();
    }

    public async Task AddAsync(DocumentoProcesado procesado)
    {
        await _context.DocumentosProcesados.AddAsync(procesado);
    }

    public void Update(DocumentoProcesado procesado)
    {
        _context.DocumentosProcesados.Update(procesado);
    }

    public async Task AddChunksAsync(IEnumerable<DocumentoChunk> chunks)
    {
        await _context.DocumentoChunks.AddRangeAsync(chunks);
    }

    public async Task DeleteChunksByProcesadoIdAsync(int procesadoId)
    {
        var chunks = await _context.DocumentoChunks
            .Where(c => c.IdDocumentoProcesado == procesadoId)
            .ToListAsync();
        if (chunks.Any())
        {
            _context.DocumentoChunks.RemoveRange(chunks);
        }
    }

    public async Task<int> GetTotalChunksByVersionIdAsync(int versionId)
    {
        return await _context.DocumentoChunks
            .Where(c => c.DocumentoProcesado!.IdVersionDocumento == versionId)
            .CountAsync();
    }
}
