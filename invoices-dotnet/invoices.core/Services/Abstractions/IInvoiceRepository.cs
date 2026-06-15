using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using invoices.core.Models;

namespace invoices.core.Services.Abstractions;

public interface IInvoiceRepository
{
    Task<List<Invoice>> GetAllAsync(
        int page, int pageSize,
        string? search = null,
        string? sortBy = null,
        bool ascending = false,
        int? year = null,
        int? month = null,
        CancellationToken ct = default);

    Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Invoice?> GetByIdTrackedAsync(Guid id, CancellationToken ct = default);

    Task<int> GetCountAsync(string? search = null, int? year = null, int? month = null, CancellationToken ct = default);

    Task AddAsync(Invoice invoice, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<Invoice> invoices, CancellationToken ct = default);

    Task UpdateAsync(Invoice invoice, CancellationToken ct = default);

    Task DeleteAsync(Invoice invoice, CancellationToken ct = default);

    Task DeleteManyAsync(List<Guid> ids, CancellationToken ct = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    Task AddRawInvoiceAsync(RawInvoice rawInvoice, CancellationToken ct = default);

    Task<RawInvoice?> GetRawInvoiceByInvoiceIdAsync(Guid invoiceId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<List<YearMonthGroup>> GetGroupsAsync(CancellationToken ct = default);

    Task<List<Invoice>> GetByMonthAsync(int year, int month, CancellationToken ct = default);
}
