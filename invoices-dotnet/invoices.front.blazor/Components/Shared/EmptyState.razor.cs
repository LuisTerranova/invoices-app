using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace invoices.front.blazor.Components.Shared;

public partial class EmptyState : ComponentBase
{
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Inbox;

    [Parameter]
    public string Message { get; set; } = "Nenhum dado encontrado";

    [Parameter]
    public string? Description { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
