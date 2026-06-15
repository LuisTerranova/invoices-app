using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace invoices.front.blazor.Components.Shared;

public partial class ConfirmDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string Title { get; set; } = "Confirmação";

    [Parameter]
    public string Message { get; set; } = string.Empty;

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
    private void Cancel() => MudDialog.Cancel();
}
