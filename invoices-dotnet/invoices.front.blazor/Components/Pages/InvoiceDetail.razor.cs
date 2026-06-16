using invoices.core.Models;
using invoices.front.blazor.Components.Shared;
using invoices.front.blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace invoices.front.blazor.Components.Pages;

public partial class InvoiceDetail : ComponentBase
{
    [Parameter]
    public Guid Id { get; set; }

    [Inject]
    private InvoiceService InvoiceService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IDialogService Dialog { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private Invoice? _invoice;
    private bool _isLoading = true;
    private bool _isEditing;
    private bool _isSaving;
    private bool _isOpeningOriginal;
    private string? _errorMessage;

    private MudForm _editForm = null!;
    private bool _isEditFormValid;

    private string _editEstablishment = string.Empty;
    private string? _editCnpj;
    private DateTime? _editDate;
    private decimal? _editTotal;
    private List<ParsedItem> _editableItems = [];
    private ParsedItem? _selectedItem;

    protected override async Task OnInitializedAsync()
    {
        await LoadInvoice();
    }

    private async Task LoadInvoice()
    {
        _isLoading = true;
        _errorMessage = null;

        try
        {
            _invoice = await InvoiceService.GetByIdAsync(Id);
            if (_invoice is null)
            {
                _errorMessage = "Nota fiscal não encontrada.";
            }
            else
            {
                _editEstablishment = _invoice.RawEstablishment ?? _invoice.Establishment?.Name ?? "";
                _editCnpj = _invoice.RawCnpj ?? _invoice.Establishment?.Cnpj;
                _editDate = _invoice.Date;
                _editTotal = _invoice.Total;
                _editableItems = _invoice.Items?.Select(i => new ParsedItem
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Total = i.Total,
                }).ToList() ?? [];
            }
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void HandleEdit()
    {
        if (_invoice is null) return;

        _editEstablishment = _invoice.RawEstablishment ?? _invoice.Establishment?.Name ?? "";
        _editCnpj = _invoice.RawCnpj ?? _invoice.Establishment?.Cnpj;
        _editDate = _invoice.Date;
        _editTotal = _invoice.Total;
        _editableItems = _invoice.Items?.Select(i => new ParsedItem
        {
            Name = i.Name,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Total = i.Total,
        }).ToList() ?? [];
        _isEditing = true;
    }

    private async Task HandleSave()
    {
        if (_invoice is null) return;

        await _editForm.Validate();
        if (!_isEditFormValid)
        {
            Snackbar.Add("Corrija os campos obrigatórios antes de salvar.", Severity.Warning);
            return;
        }

        _isSaving = true;
        try
        {
            _invoice.RawEstablishment = _editEstablishment;
            _invoice.RawCnpj = _editCnpj;
            _invoice.Date = _editDate;
            _invoice.Total = _editTotal;
            _invoice.Items = _editableItems.Where(i => !string.IsNullOrWhiteSpace(i.Name)).ToList();

            await InvoiceService.UpdateAsync(_invoice);
            Snackbar.Add("Nota fiscal atualizada com sucesso.", Severity.Success);
            _isEditing = false;
            await LoadInvoice();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erro ao salvar: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void HandleCancel()
    {
        _isEditing = false;
        // Restore view-mode items
        _editableItems = _invoice?.Items?.Select(i => new ParsedItem
        {
            Name = i.Name,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Total = i.Total,
        }).ToList() ?? [];
    }

    private async Task HandleDelete()
    {
        if (_invoice is null) return;

        var confirmed = await ShowConfirmDialog(
            "Excluir Nota Fiscal",
            $"Deseja realmente excluir a nota de \"{_invoice.RawEstablishment}\"? Esta ação não pode ser desfeita.");

        if (confirmed)
        {
            try
            {
                await InvoiceService.DeleteAsync(_invoice.Id);
                Snackbar.Add("Nota fiscal excluída com sucesso.", Severity.Success);
                Navigation.NavigateTo("/");
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erro ao excluir: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task ViewOriginal()
    {
        if (_invoice is null) return;

        _isOpeningOriginal = true;
        try
        {
            var data = await InvoiceService.GetRawImageAsync(_invoice.Id);
            if (data is null)
            {
                Snackbar.Add("PDF original não encontrado.", Severity.Warning);
                return;
            }

            var base64 = Convert.ToBase64String(data);
            await JS.InvokeVoidAsync("window.open", $"data:application/pdf;base64,{base64}");
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erro ao abrir PDF: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isOpeningOriginal = false;
        }
    }

    private void AddItem()
    {
        _editableItems.Add(new ParsedItem());
    }

    private void RemoveItem(ParsedItem item)
    {
        _editableItems.Remove(item);
    }

    private void GoBack()
    {
        Navigation.NavigateTo("/");
    }

    private async Task<bool> ShowConfirmDialog(string title, string message)
    {
        var parameters = new DialogParameters
        {
            { "Message", message }
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
        };
        var dialog = await Dialog.ShowAsync<ConfirmDialog>(title, parameters, options);
        var result = await dialog.Result;
        return !result.Canceled;
    }
}
