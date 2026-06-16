using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using invoices.core.Models;

namespace invoices.core.Services.Abstractions;

public interface IInvoiceService
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

    Task<int> GetCountAsync(string? search = null, int? year = null, int? month = null, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task DeleteManyAsync(List<Guid> ids, CancellationToken ct = default);

    Task UpdateAsync(Invoice invoice, CancellationToken ct = default);

    Task SendInvoicesToProcessAsync(RawInvoice raw, CancellationToken ct = default);

    Task<List<YearMonthGroup>> GetGroupsAsync(CancellationToken ct = default);

    Task<List<Invoice>> GetByMonthAsync(int year, int month, CancellationToken ct = default);

    Task<List<Invoice>> GetByIdsAsync(List<Guid> ids, CancellationToken ct = default);
}
