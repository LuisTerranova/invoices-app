using invoices.core.Models;
using invoices.front.blazor.Components.Shared;
using invoices.front.blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace invoices.front.blazor.Components.Pages;

public partial class InvoiceList : ComponentBase
{
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

    private List<Invoice> _invoices = [];
    private List<YearMonthGroup> _groups = [];
    private HashSet<Invoice> _selectedInvoices = [];

    private void OnSelectedItemsChanged(HashSet<Invoice> items)
    {
        _selectedInvoices = items ?? [];
        StateHasChanged();
    }
    private YearMonthGroup? _selectedGroup;
    private string _searchText = string.Empty;
    private int _currentPage = 1;
    private int _totalCount;
    private int _pageSize = 20;
    private bool _isInitialLoading = true;
    private bool _isExporting;
    private string? _errorMessage;

    private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)_totalCount / _pageSize));
    private bool _canGoPrevious => _currentPage > 1;
    private bool _canGoNext => _currentPage < TotalPages;
    private bool _hasSelection => _selectedInvoices.Count > 0;

    protected override async Task OnInitializedAsync()
    {
        await LoadData();
    }

    private async Task LoadData()
    {
        _isInitialLoading = true;
        _errorMessage = null;

        try
        {
            var loadGroups = _groups.Count == 0
                ? InvoiceService.GetGroupsAsync()
                : Task.FromResult(_groups);

            var loadInvoices = InvoiceService.GetAllAsync(
                _currentPage, _pageSize,
                string.IsNullOrWhiteSpace(_searchText) ? null : _searchText,
                null, false,
                _selectedGroup?.Year, _selectedGroup?.Month);

            var loadCount = InvoiceService.GetCountAsync(
                string.IsNullOrWhiteSpace(_searchText) ? null : _searchText,
                _selectedGroup?.Year, _selectedGroup?.Month);

            await Task.WhenAll(loadGroups, loadInvoices, loadCount);

            _groups = await loadGroups;
            _invoices = await loadInvoices;
            _totalCount = await loadCount;
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
        }
        finally
        {
            _isInitialLoading = false;
        }
    }

    private async Task HandleRefresh()
    {
        _currentPage = 1;
        _searchText = string.Empty;
        _selectedGroup = null;
        _groups.Clear();
        await LoadData();
        Snackbar.Add("Lista atualizada.", Severity.Normal);
    }

    private async Task HandleSearch()
    {
        _currentPage = 1;
        await LoadData();
    }

    private async Task HandleSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await HandleSearch();
        }
    }

    private async Task HandleClearSearch()
    {
        _searchText = string.Empty;
        _currentPage = 1;
        await LoadData();
    }

    private async Task SelectGroup(YearMonthGroup? group)
    {
        _selectedGroup = group;
        _currentPage = 1;
        await LoadData();
    }

    private async Task GoToNextPage()
    {
        if (!_canGoNext) return;
        _currentPage++;
        await LoadData();
    }

    private async Task GoToPreviousPage()
    {
        if (!_canGoPrevious) return;
        _currentPage--;
        await LoadData();
    }

    private async Task HandlePageChanged(int page)
    {
        if (_currentPage == page) return;
        _currentPage = page;
        await LoadData();
    }

    private async Task HandleRowClick(TableRowClickEventArgs<Invoice> args)
    {
        if (args.Item is not null)
        {
            Navigation.NavigateTo($"/invoices/{args.Item.Id}");
        }
    }

    private void NavigateToDetail(Invoice invoice)
    {
        Navigation.NavigateTo($"/invoices/{invoice.Id}");
    }

    private async Task OpenDeleteDialog()
    {
        var idsToDelete = _selectedInvoices.Select(i => i.Id).ToList();
        if (idsToDelete.Count == 0) return;

        var confirmed = await ShowConfirmDialog(
            "Excluir Notas Fiscais",
            $"Deseja realmente excluir {idsToDelete.Count} nota(s) fiscal(is)? Esta ação não pode ser desfeita.");

        if (confirmed)
        {
            try
            {
                await InvoiceService.DeleteManyAsync(idsToDelete);
                Snackbar.Add($"{idsToDelete.Count} nota(s) excluída(s) com sucesso.", Severity.Success);
                _selectedInvoices.Clear();
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erro ao excluir: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task OpenSingleDeleteDialog(Invoice invoice)
    {
        var confirmed = await ShowConfirmDialog(
            "Excluir Nota Fiscal",
            $"Deseja realmente excluir a nota de \"{invoice.RawEstablishment}\"? Esta ação não pode ser desfeita.");

        if (confirmed)
        {
            try
            {
                await InvoiceService.DeleteAsync(invoice.Id);
                Snackbar.Add("Nota fiscal excluída com sucesso.", Severity.Success);
                await LoadData();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erro ao excluir: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task HandleExport()
    {
        if (_selectedGroup is null && _selectedInvoices.Count == 0) return;

        _isExporting = true;
        try
        {
            Stream stream;
            string fileName;

            if (_selectedInvoices.Count > 0)
            {
                var ids = _selectedInvoices.Select(i => i.Id).ToList();
                (stream, fileName) = await InvoiceService.DownloadExportAsync(null, null, ids);
            }
            else
            {
                (stream, fileName) = await InvoiceService.DownloadExportAsync(_selectedGroup!.Year, _selectedGroup.Month, null);
            }

            using var streamRef = new DotNetStreamReference(stream);
            await JS.InvokeVoidAsync("downloadFileFromStream", fileName, streamRef);
            
            Snackbar.Add("Relatório exportado com sucesso!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erro ao exportar relatório: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isExporting = false;
        }
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
        return result is not null && !result.Canceled;
    }
}
