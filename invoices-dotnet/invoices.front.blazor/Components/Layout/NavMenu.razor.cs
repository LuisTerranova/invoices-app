using invoices.front.blazor.Services;
using Microsoft.AspNetCore.Components;

namespace invoices.front.blazor.Components.Layout;

public partial class NavMenu : ComponentBase
{
    [Inject]
    private AuthService AuthService { get; set; } = null!;

    private string _username = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        var token = await AuthService.GetTokenAsync();
        if (token is not null)
        {
            var payload = token.Split('.')[1];
            var json = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')));

            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("sub", out var sub) || doc.RootElement.TryGetProperty("unique_name", out sub))
            {
                _username = sub.GetString() ?? "";
            }
        }
    }
}
