using System.Net.Http.Json;
using System.Text.Json;
using invoices.core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace invoices.front.blazor.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json;
    private readonly ApiAuthenticationStateProvider _authStateProvider;
    private readonly IJSRuntime _js;

    private const string TokenKey = "auth_token";
    private const string RefreshTokenKey = "auth_refresh_token";
    private const string ExpiresAtKey = "auth_expires_at";

    public AuthService(
        IHttpClientFactory httpClientFactory,
        JsonSerializerOptions json,
        ApiAuthenticationStateProvider authStateProvider,
        IJSRuntime js)
    {
        _http = httpClientFactory.CreateClient("Api");
        _json = json;
        _authStateProvider = authStateProvider;
        _js = js;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _js.InvokeAsync<string>("localStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token)) return false;

        try
        {
            var expiresAt = await _js.InvokeAsync<string>("localStorage.getItem", ExpiresAtKey);
            if (expiresAt is not null && DateTime.TryParse(expiresAt, out var expiry))
            {
                return expiry > DateTime.UtcNow.AddMinutes(5);
            }
        }
        catch
        {
        }

        return true;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/auth/login",
                new { username, password }, _json);

            var result = await response.Content.ReadFromJsonAsync<AuthResult>(_json);

            if (result is not null && result.Success)
            {
                await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, result.Token ?? "");
                await _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, result.RefreshToken ?? "");
                await _js.InvokeVoidAsync("localStorage.setItem", ExpiresAtKey, result.ExpiresAt?.ToString("O") ?? "");
                _authStateProvider.NotifyStateChanged();
            }

            return result ?? new AuthResult(false, null, null, null, "Resposta inválida do servidor");
        }
        catch (Exception ex)
        {
            return new AuthResult(false, null, null, null, ex.Message);
        }
    }

    public async Task<bool> TryRefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var refreshToken = await _js.InvokeAsync<string>("localStorage.getItem", RefreshTokenKey);
            if (string.IsNullOrEmpty(refreshToken)) return false;

            var response = await _http.PostAsJsonAsync("api/auth/refresh",
                new { refresh_token = refreshToken }, _json, ct);

            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<AuthResult>(_json, ct);

            if (result is not null && result.Success)
            {
                await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, result.Token ?? "");
                await _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, result.RefreshToken ?? "");
                await _js.InvokeVoidAsync("localStorage.setItem", ExpiresAtKey, result.ExpiresAt?.ToString("O") ?? "");
                _authStateProvider.NotifyStateChanged();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var refreshToken = await _js.InvokeAsync<string>("localStorage.getItem", RefreshTokenKey);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                await _http.PostAsJsonAsync("api/auth/logout",
                    new { refresh_token = refreshToken }, _json);
            }
        }
        catch
        {
        }
        finally
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
            await _js.InvokeVoidAsync("localStorage.removeItem", ExpiresAtKey);
            _authStateProvider.NotifyStateChanged();
        }
    }
}
