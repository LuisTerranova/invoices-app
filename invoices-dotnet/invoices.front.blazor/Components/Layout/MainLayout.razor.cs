using invoices.front.blazor.Services;
using Microsoft.AspNetCore.Components;

namespace invoices.front.blazor.Components.Layout;

public partial class MainLayout : LayoutComponentBase
{
    [Inject]
    private AuthService AuthService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private async Task HandleLogout()
    {
        await AuthService.LogoutAsync();
        Navigation.NavigateTo("/login", forceLoad: true);
    }
}
