using System.Text;
using System.Text.Json;
using invoices.api.Data.Context;
using invoices.core.Models;
using invoices.core.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace invoices.api.Services.Implementations;

public class InvoiceService(
    IInvoiceRepository repo,
    AppDbContext db,
    IChannel channel,
    JsonSerializerOptions jsonOptions,
    ILogger<InvoiceService> _logger
) : IInvoiceService
{
    public async Task<List<Invoice>> GetAllAsync(
        int page,
        int pageSize,
        string? search = null,
        string? sortBy = null,
        bool ascending = false,
        int? year = null,
        int? month = null,
        CancellationToken ct = default
    )
    {
        return await repo.GetAllAsync(page, pageSize, search, sortBy, ascending, year, month, ct);
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await repo.GetByIdAsync(id, ct);
    }

    public async Task<int> GetCountAsync(string? search = null, int? year = null, int? month = null, CancellationToken ct = default)
    {
        return await repo.GetCountAsync(search, year, month, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await repo.GetByIdAsync(id, ct);

        if (invoice is null)
            throw new KeyNotFoundException($"Invoice with id '{id}' was not found.");

        await repo.DeleteAsync(invoice, ct);
    }

    public async Task DeleteManyAsync(List<Guid> ids, CancellationToken ct = default)
    {
        await repo.DeleteManyAsync(ids, ct);
    }

    public Task<List<YearMonthGroup>> GetGroupsAsync(CancellationToken ct = default)
    {
        return repo.GetGroupsAsync(ct);
    }

    public Task<List<Invoice>> GetByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        return repo.GetByMonthAsync(year, month, ct);
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        _logger.LogInformation("=== UPDATE INVOICE {Id} ===", invoice.Id);
        _logger.LogInformation("Payload items count: {Count}", invoice.Items?.Count ?? 0);

        foreach (var item in invoice.Items ?? [])
        {
            _logger.LogInformation("  Payload item: Name='{Name}' Qty={Qty} Price={Price} Total={Total}",
                item.Name, item.Quantity, item.UnitPrice, item.Total);
        }

        var existingInvoice = await repo.GetByIdTrackedAsync(invoice.Id, ct);
        if (existingInvoice == null)
            throw new KeyNotFoundException($"Invoice with id '{invoice.Id}' was not found.");

        _logger.LogInformation("Existing items count before update: {Count}", existingInvoice.Items?.Count ?? 0);

        if (!string.IsNullOrWhiteSpace(invoice.RawEstablishment) || !string.IsNullOrWhiteSpace(invoice.RawCnpj))
        {
            var est = await db.Establishments.FirstOrDefaultAsync(e => 
                (!string.IsNullOrWhiteSpace(invoice.RawCnpj) && e.Cnpj == invoice.RawCnpj) || 
                (!string.IsNullOrWhiteSpace(invoice.RawEstablishment) && e.Name == invoice.RawEstablishment), ct);
            
            if (est == null)
            {
                est = new Establishment 
                { 
                    Name = string.IsNullOrWhiteSpace(invoice.RawEstablishment) ? "Unknown" : invoice.RawEstablishment, 
                    Cnpj = invoice.RawCnpj 
                };
                await db.Establishments.AddAsync(est, ct);
            }
            
            existingInvoice.EstablishmentId = est.Id;
            existingInvoice.Establishment = est;
        }
        else if (invoice.EstablishmentId.HasValue)
        {
            existingInvoice.EstablishmentId = invoice.EstablishmentId;
        }

        existingInvoice.Items!.Clear();
        _logger.LogInformation("Items cleared. Adding {Count} new items...", invoice.Items?.Count ?? 0);

        foreach (var item in invoice.Items ?? [])
        {
            var newItem = new ParsedItem
            {
                Name = item.Name,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Total = item.Total,
            };
            existingInvoice.Items.Add(newItem);
            _logger.LogInformation("  Added: Name='{Name}' Qty={Qty} Price={Price} Total={Total}",
                newItem.Name, newItem.Quantity, newItem.UnitPrice, newItem.Total);
        }

        existingInvoice.Date = invoice.Date;
        existingInvoice.Total = invoice.Total;

        _logger.LogInformation("Existing items count after update: {Count}", existingInvoice.Items.Count);
        _logger.LogInformation("Saving changes (entity is tracked, not calling Update())...");

        await repo.SaveChangesAsync(ct);
        _logger.LogInformation("=== UPDATE COMPLETE ===");
    }

    public async Task SendInvoicesToProcessAsync(RawInvoice raw, CancellationToken ct = default)
    {
        await repo.AddRawInvoiceAsync(raw, ct);
        await repo.SaveChangesAsync(ct);

        await channel.QueueDeclareAsync(
            queue: "invoices_to_process",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct
        );

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(raw, jsonOptions));

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "invoices_to_process",
            body: body,
            cancellationToken: ct
        );
    }
}
