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

public class DocumentoRepository : IDocumentoRepository
{
    private readonly AsistenteDbContext _context;

    public DocumentoRepository(AsistenteDbContext context)
    {
        _context = context;
    }

    public async Task<Documento?> GetByIdAsync(int id)
    {
        return await _context.Set<Documento>()
            .Include(d => d.Categoria)
            .FirstOrDefaultAsync(d => d.IdDocumento == id);
    }

    public async Task<Documento?> GetByIdWithVersionesAsync(int id)
    {
        return await _context.Set<Documento>()
            .Include(d => d.Categoria)
            .Include(d => d.Versiones.OrderByDescending(v => v.NumeroVersion))
            .FirstOrDefaultAsync(d => d.IdDocumento == id);
    }

    public async Task<Documento?> GetByCodigoAsync(string codigo)
    {
        return await _context.Set<Documento>()
            .Include(d => d.Categoria)
            .FirstOrDefaultAsync(d => d.Codigo.ToLower() == codigo.ToLower());
    }

    public async Task<IEnumerable<Documento>> GetAllAsync()
    {
        return await _context.Set<Documento>()
            .Include(d => d.Categoria)
            .Where(d => d.Estado != EstadoDocumento.Eliminado)
            .ToListAsync();
    }

    public async Task<IEnumerable<Documento>> GetFilteredAsync(
        string? nombre,
        int? idCategoria,
        EstadoDocumento? estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta)
    {
        var query = _context.Set<Documento>()
            .Include(d => d.Categoria)
            .AsQueryable();

        // No mostrar eliminados a menos que se pida explícitamente, o por defecto excluirlos
        if (estado.HasValue)
        {
            query = query.Where(d => d.Estado == estado.Value);
        }
        else
        {
            query = query.Where(d => d.Estado != EstadoDocumento.Eliminado);
        }

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            query = query.Where(d => d.Nombre.Contains(nombre) || d.Codigo.Contains(nombre));
        }

        if (idCategoria.HasValue && idCategoria.Value > 0)
        {
            query = query.Where(d => d.IdCategoria == idCategoria.Value);
        }

        if (fechaDesde.HasValue)
        {
            query = query.Where(d => d.FechaRegistro >= fechaDesde.Value);
        }

        if (fechaHasta.HasValue)
        {
            // Ajustar al final del día
            var limiteHasta = fechaHasta.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(d => d.FechaRegistro <= limiteHasta);
        }

        return await query.OrderByDescending(d => d.FechaRegistro).ToListAsync();
    }

    /// <summary>
    /// Contenido real ya indexado de cada documento Activo: código, nombre y el
    /// texto de sus fragmentos concatenado (tope en maxCharsPorDocumento). Alimenta
    /// la decisión semántica de ruta RAG del Planner y del rescate por catálogo.
    /// Solo documentos con al menos un embedding generado.
    /// </summary>
    public async Task<IEnumerable<DocumentoContenidoResumen>> GetContenidosIndexadosAsync(int maxCharsPorDocumento)
    {
        var documentos = await _context.Set<Documento>()
            .Where(d => d.Estado == EstadoDocumento.Activo)
            .ToListAsync();
        if (documentos.Count == 0) return Array.Empty<DocumentoContenidoResumen>();

        var ids = documentos.Select(d => d.IdDocumento).ToList();
        var versiones = await _context.Set<DocumentoVersion>()
            .Where(v => ids.Contains(v.IdDocumento))
            .Select(v => new { v.IdVersion, v.IdDocumento, v.Activo })
            .ToListAsync();

        var idsVersion = versiones.Select(v => v.IdVersion).ToList();
        var procesados = await _context.Set<DocumentoProcesado>()
            .Where(p => idsVersion.Contains(p.IdVersionDocumento) && p.Estado == EstadoProcesamiento.Procesado)
            .Select(p => new { p.IdDocumentoProcesado, p.IdVersionDocumento })
            .ToListAsync();

        var idsProcesado = procesados.Select(p => p.IdDocumentoProcesado).ToList();
        var indexados = await _context.Set<DocumentoIndexado>()
            .Where(i => idsProcesado.Contains(i.IdDocumentoProcesado))
            .Select(i => new { i.IdDocumentoProcesado })
            .ToListAsync();
        var indexadosSet = indexados.Select(i => i.IdDocumentoProcesado).ToHashSet();

        // Un solo chunk por documento procesado: el más reciente gana.
        var procesadoPorDocumento = procesados
            .Where(p => indexadosSet.Contains(p.IdDocumentoProcesado))
            .GroupBy(p => versiones.First(v => v.IdVersion == p.IdVersionDocumento).IdDocumento)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.IdDocumentoProcesado).First().IdDocumentoProcesado);

        if (procesadoPorDocumento.Count == 0) return Array.Empty<DocumentoContenidoResumen>();

        var idsProcesadoFinal = procesadoPorDocumento.Values.ToList();
        var chunks = await _context.Set<DocumentoChunk>()
            .Where(c => idsProcesadoFinal.Contains(c.IdDocumentoProcesado))
            .OrderBy(c => c.Orden)
            .Select(c => new { c.IdDocumentoProcesado, c.Texto })
            .ToListAsync();

        var porProcesado = chunks
            .GroupBy(c => c.IdDocumentoProcesado)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Texto).ToList());

        var resultado = new List<DocumentoContenidoResumen>();
        foreach (var d in documentos)
        {
            if (!procesadoPorDocumento.TryGetValue(d.IdDocumento, out var idProcesado)) continue;
            if (!porProcesado.TryGetValue(idProcesado, out var textos)) continue;

            var sb = new System.Text.StringBuilder();
            foreach (var t in textos)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                if (sb.Length + t.Length > maxCharsPorDocumento) break;
                sb.Append(t).Append('\n');
            }
            if (sb.Length == 0) continue;

            resultado.Add(new DocumentoContenidoResumen
            {
                IdDocumento = d.IdDocumento,
                Codigo = d.Codigo ?? string.Empty,
                Nombre = d.Nombre ?? string.Empty,
                Descripcion = d.Descripcion,
                Texto = sb.ToString(),
                TotalChunks = textos.Count
            });
        }
        return resultado;
    }

    /// <summary>
    /// Nombre de archivo de la versión vigente de cada documento Activo.
    /// </summary>
    public async Task<IEnumerable<(int IdDocumento, string NombreArchivo)>> GetNombresArchivoAsync()
    {
        var filas = await (from d in _context.Set<Documento>()
                           join v in _context.Set<DocumentoVersion>()
                               on d.IdDocumento equals v.IdDocumento
                           where d.Estado == EstadoDocumento.Activo
                               && v.Activo
                               && v.NombreArchivo != null
                               && v.NombreArchivo != ""
                           orderby v.IdVersion descending
                           select new { d.IdDocumento, v.NombreArchivo })
                           .ToListAsync();
        return filas
            .GroupBy(f => f.IdDocumento)
            .Select(g => (g.Key, g.First().NombreArchivo!))
            .ToList();
    }

    public async Task AddAsync(Documento documento)
    {
        await _context.Set<Documento>().AddAsync(documento);
    }

    public void Update(Documento documento)
    {
        _context.Set<Documento>().Update(documento);
    }
}
