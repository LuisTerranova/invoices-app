using System.Net.Http.Json;
using System.Text.Json;
using invoices.core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace invoices.front.blazor.Services;

public class InvoiceService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;

    public InvoiceService(IHttpClientFactory httpClientFactory, JsonSerializerOptions json)
    {
        _http = httpClientFactory.CreateClient("Api");
        _json = json;
    }

    public async Task<List<Invoice>> GetAllAsync(
        int page, int pageSize,
        string? search = null,
        string? sortBy = null,
        bool ascending = false,
        int? year = null,
        int? month = null,
        CancellationToken ct = default)
    {
        var query = $"?page={page}&pageSize={pageSize}";
        if (search is not null) query += $"&search={Uri.EscapeDataString(search)}";
        if (sortBy is not null) query += $"&sortBy={sortBy}";
        query += $"&ascending={ascending.ToString().ToLower()}";
        if (year.HasValue) query += $"&year={year}";
        if (month.HasValue) query += $"&month={month}";

        var response = await _http.GetAsync($"api/invoices{query}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Invoice>>(_json, ct) ?? [];
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/invoices/{id}", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<Invoice>(_json, ct);
    }

    public async Task<int> GetCountAsync(
        string? search = null,
        int? year = null,
        int? month = null,
        CancellationToken ct = default)
    {
        var query = "";
        if (search is not null) query += $"?search={Uri.EscapeDataString(search)}";
        if (year.HasValue) query += $"{(query == "" ? "?" : "&")}year={year}";
        if (month.HasValue) query += $"{(query == "" ? "?" : "&")}month={month}";

        var response = await _http.GetAsync($"api/invoices/count{query}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>(_json, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/invoices/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteManyAsync(List<Guid> ids, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/invoices/batch-delete",
            new BatchDeleteRequest { Ids = ids }, _json, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAsync(Invoice invoice, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/invoices/{invoice.Id}", invoice, _json, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendInvoicesToProcessAsync(RawInvoice raw, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/invoices/process", raw, _json, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<YearMonthGroup>> GetGroupsAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("api/invoices/groups", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<YearMonthGroup>>(_json, ct) ?? [];
    }

    public async Task<List<Invoice>> GetByMonthAsync(int year, int month, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/invoices/by-month?year={year}&month={month}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Invoice>>(_json, ct) ?? [];
    }

    public async Task<(Stream Stream, string FileName)> DownloadExportAsync(int? year, int? month, List<Guid>? ids, CancellationToken ct = default)
    {
        var query = "";
        if (ids is { Count: > 0 })
        {
            query = $"?ids={string.Join(",", ids)}";
        }
        else if (year.HasValue && month.HasValue)
        {
            query = $"?year={year}&month={month}";
        }
        else
        {
            throw new ArgumentException("Either year and month, or a list of ids must be provided.");
        }

        var response = await _http.GetAsync($"api/invoices/export{query}", ct);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        var fileName = response.Content.Headers.ContentDisposition?.FileName ?? "relatorio.xlsx";
        fileName = fileName.Trim('"');

        return (stream, fileName);
    }

    public async Task<byte[]?> GetRawImageAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"api/invoices/{id}/raw-image", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
