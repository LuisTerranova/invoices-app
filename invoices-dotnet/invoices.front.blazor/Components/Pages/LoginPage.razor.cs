using invoices.front.blazor.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace invoices.front.blazor.Components.Pages;

public partial class LoginPage : ComponentBase
{
    [Inject]
    private AuthService AuthService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string? _errorMessage;
    private bool _isLoading;

    protected override async Task OnInitializedAsync()
    {
        var isAuth = await AuthService.IsAuthenticatedAsync();
        if (isAuth)
        {
            Navigation.NavigateTo("/", forceLoad: true);
        }
    }

    private async Task HandleLogin()
    {
        if (string.IsNullOrWhiteSpace(_username) || string.IsNullOrWhiteSpace(_password))
        {
            _errorMessage = "Preencha usuário e senha.";
            return;
        }

        _isLoading = true;
        _errorMessage = null;

        try
        {
            var result = await AuthService.LoginAsync(_username, _password);

            if (result.Success)
            {
                Snackbar.Add("Login realizado com sucesso!", Severity.Success);
                Navigation.NavigateTo("/", forceLoad: true);
            }
            else
            {
                _errorMessage = result.ErrorMessage ?? "Usuário ou senha inválidos.";
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Erro de conexão: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }
}
