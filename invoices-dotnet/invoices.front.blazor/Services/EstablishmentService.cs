using System.Net.Http.Json;
using System.Text.Json;
using invoices.core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace invoices.front.blazor.Services;

public class EstablishmentService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;

    public EstablishmentService(IHttpClientFactory httpClientFactory, JsonSerializerOptions json)
    {
        _http = httpClientFactory.CreateClient("Api");
        _json = json;
    }

    public async Task<List<Establishment>> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var response = await _http.GetAsync(
            $"api/establishments/search?q={Uri.EscapeDataString(query)}", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<Establishment>>(_json, ct) ?? [];
    }
}
