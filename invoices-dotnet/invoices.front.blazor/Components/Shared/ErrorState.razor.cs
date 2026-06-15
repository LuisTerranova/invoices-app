using Microsoft.AspNetCore.Components;

namespace invoices.front.blazor.Components.Shared;

public partial class ErrorState : ComponentBase
{
    [Parameter]
    public string Message { get; set; } = "Ocorreu um erro ao carregar os dados";

    [Parameter]
    public string? Description { get; set; }

    [Parameter]
    public bool ShowRetry { get; set; } = true;

    [Parameter]
    public EventCallback OnRetry { get; set; }
}
