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

public class DocumentoIndexadoRepository : IDocumentoIndexadoRepository
{
    private readonly AsistenteDbContext _context;

    public DocumentoIndexadoRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<DocumentoIndexado?> GetByIdAsync(int id)
    {
        return await _context.DocumentosIndexados
            .Include(i => i.DocumentoProcesado)
                .ThenInclude(p => p!.VersionDocumento)
                    .ThenInclude(v => v!.Documento)
            .FirstOrDefaultAsync(i => i.IdDocumentoIndexado == id);
    }

    public async Task<DocumentoIndexado?> GetByProcesadoIdAsync(int documentoProcesadoId)
    {
        return await _context.DocumentosIndexados
            .Include(i => i.DocumentoProcesado)
                .ThenInclude(p => p!.VersionDocumento)
                    .ThenInclude(v => v!.Documento)
            .FirstOrDefaultAsync(i => i.IdDocumentoProcesado == documentoProcesadoId);
    }

    public async Task<IEnumerable<DocumentoIndexado>> GetAllAsync()
    {
        return await _context.DocumentosIndexados
            .Include(i => i.DocumentoProcesado)
                .ThenInclude(p => p!.VersionDocumento)
                    .ThenInclude(v => v!.Documento)
            .OrderByDescending(i => i.FechaIndexacion)
            .ToListAsync();
    }

    public async Task<IEnumerable<DocumentoIndexado>> GetByEstadoAsync(EstadoIndexacion estado)
    {
        return await _context.DocumentosIndexados
            .Include(i => i.DocumentoProcesado)
                .ThenInclude(p => p!.VersionDocumento)
                    .ThenInclude(v => v!.Documento)
            .Where(i => i.Estado == estado)
            .OrderByDescending(i => i.FechaIndexacion)
            .ToListAsync();
    }

    public async Task AddAsync(DocumentoIndexado indexado)
    {
        await _context.DocumentosIndexados.AddAsync(indexado);
    }

    public void Update(DocumentoIndexado indexado)
    {
        _context.DocumentosIndexados.Update(indexado);
    }

    public void Delete(DocumentoIndexado indexado)
    {
        _context.DocumentosIndexados.Remove(indexado);
    }

    public async Task<int> GetTotalDocumentosIndexadosAsync()
    {
        return await _context.DocumentosIndexados
            .CountAsync(i => i.Estado == EstadoIndexacion.Indexado);
    }

    public async Task<int> GetTotalChunksIndexadosAsync()
    {
        return await _context.DocumentosIndexados
            .Where(i => i.Estado == EstadoIndexacion.Indexado)
            .SumAsync(i => i.TotalChunks);
    }

    public async Task<double> GetTiempoPromedioIndexacionAsync()
    {
        var indexados = await _context.DocumentosIndexados
            .Where(i => i.Estado == EstadoIndexacion.Indexado && i.DocumentoProcesado != null)
            .Select(i => new
            {
                i.FechaIndexacion,
                i.DocumentoProcesado!.FechaInicio
            })
            .ToListAsync();

        if (!indexados.Any()) return 0;

        var tiempos = indexados
            .Select(i => (i.FechaIndexacion - i.FechaInicio).TotalMilliseconds)
            .Where(t => t > 0);

        return tiempos.Any() ? tiempos.Average() : 0;
    }
}
