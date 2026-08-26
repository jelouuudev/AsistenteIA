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

    public async Task AddAsync(Documento documento)
    {
        await _context.Set<Documento>().AddAsync(documento);
    }

    public void Update(Documento documento)
    {
        _context.Set<Documento>().Update(documento);
    }
}
