using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using invoices.api.Data.Context;
using invoices.core.Models;
using invoices.core.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace invoices.api.Data.Repositories;

public class InvoiceRepository(AppDbContext db) : IInvoiceRepository
{
    private static readonly Dictionary<string, Expression<Func<Invoice, object>>> SortExpressions =
        new()
        {
            ["date"] = i => (object?)i.Date,
            ["total"] = i => (object)i.Total!,
            ["establishment"] = i => (object?)i.Establishment!.Name,
            ["cnpj"] = i => (object?)i.Establishment!.Cnpj,
            ["is_valid"] = i => (object)i.IsValid,
            ["created_at"] = i => (object)i.RawInvoice!.CreatedAt!,
        };

    public async Task<List<Invoice>> GetAllAsync(
        int page,
        int pageSize,
        string? search = null,
        string? sortBy = null,
        bool ascending = false,
        int? year = null,
        int? month = null,
        CancellationToken ct = default)
    {
        var query = BuildSearchQuery(
            db.Invoices.Include(i => i.Establishment).Include(i => i.Items).AsNoTracking(),
            search, year, month);

        query = ApplySorting(query, sortBy, ascending);

        return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Invoices.Include(i => i.Establishment).Include(i => i.Items).AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<Invoice?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Invoices.Include(i => i.Establishment).Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<int> GetCountAsync(string? search = null, int? year = null, int? month = null, CancellationToken ct = default)
    {
        var query = BuildSearchQuery(db.Invoices.AsNoTracking(), search, year, month);
        return await query.CountAsync(ct);
    }

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        await db.Invoices.AddAsync(invoice, ct);
    }

    public async Task AddRangeAsync(IEnumerable<Invoice> invoices, CancellationToken ct = default)
    {
        await db.Invoices.AddRangeAsync(invoices, ct);
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        db.Invoices.Update(invoice);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Invoice invoice, CancellationToken ct = default)
    {
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteManyAsync(List<Guid> ids, CancellationToken ct = default)
    {
        await db.Invoices.Where(i => ids.Contains(i.Id)).ExecuteDeleteAsync(ct);
    }

    public async Task AddRawInvoiceAsync(RawInvoice rawInvoice, CancellationToken ct = default)
    {
        await db.RawInvoices.AddAsync(rawInvoice, ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await db.Invoices.AnyAsync(i => i.Id == id, ct);
    }

    public async Task<RawInvoice?> GetRawInvoiceByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        return await db.Invoices
            .Where(i => i.Id == invoiceId)
            .Select(i => i.RawInvoice)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<YearMonthGroup>> GetGroupsAsync(CancellationToken ct = default)
    {
        return await db.Invoices
            .Where(i => i.Date != null)
            .GroupBy(i => new { i.Date!.Value.Year, i.Date!.Value.Month })
            .Select(g => new YearMonthGroup
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count()
            })
            .OrderByDescending(g => g.Year)
            .ThenByDescending(g => g.Month)
            .ToListAsync(ct);
    }

    public async Task<List<Invoice>> GetByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        return await db.Invoices
            .Include(i => i.Establishment)
            .Include(i => i.Items)
            .AsNoTracking()
            .Where(i => i.Date != null && i.Date.Value.Year == year && i.Date.Value.Month == month)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<Invoice> BuildSearchQuery(IQueryable<Invoice> query, string? search, int? year = null, int? month = null)
    {
        if (year.HasValue)
            query = query.Where(i => i.Date != null && i.Date.Value.Year == year.Value);
        if (month.HasValue)
            query = query.Where(i => i.Date != null && i.Date.Value.Month == month.Value);

        if (string.IsNullOrWhiteSpace(search))
            return query;

        var term = search.Trim().ToLowerInvariant();
        return query.Where(i =>
            (i.Establishment != null && i.Establishment.Name.ToLower().Contains(term))
            || (i.Establishment != null && i.Establishment.Cnpj != null && i.Establishment.Cnpj.Contains(term))
            || (i.AccessKey != null && i.AccessKey.Contains(term))
            || (i.RawText != null && i.RawText.ToLower().Contains(term))
        );
    }

    private static IQueryable<Invoice> ApplySorting(IQueryable<Invoice> query, string? sortBy, bool ascending)
    {
        if (string.IsNullOrWhiteSpace(sortBy) || !SortExpressions.TryGetValue(sortBy, out var expr))
            return query.OrderByDescending(i => i.Date);

        return ascending ? query.OrderBy(expr) : query.OrderByDescending(expr);
    }
}
