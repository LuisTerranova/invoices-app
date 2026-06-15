using invoices.core.Models;
using invoices.front.blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;

namespace invoices.front.blazor.Components.Pages;

public partial class InvoiceUpload : ComponentBase
{
    [Inject]
    private InvoiceService InvoiceService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private List<PendingFile> _pendingFiles = [];
    private bool _isUploading;
    private int _uploadedCount;
    private string? _statusMessage;
    private bool _statusIsError;

    private class PendingFile
    {
        public string FileName { get; init; } = string.Empty;
        public byte[]? Data { get; set; }
        public string? Error { get; set; }
    }

    private async Task HandleFilesSelected(InputFileChangeEventArgs args)
    {
        foreach (var file in args.GetMultipleFiles(maximumFileCount: 50))
        {
            try
            {
                if (!file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    _statusMessage = $"Arquivo ignorado (não é PDF): {file.Name}";
                    _statusIsError = true;
                    continue;
                }

                using var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);

                _pendingFiles.Add(new PendingFile
                {
                    FileName = file.Name,
                    Data = ms.ToArray(),
                });
            }
            catch (Exception ex)
            {
                _pendingFiles.Add(new PendingFile
                {
                    FileName = file.Name,
                    Error = $"Erro ao ler: {ex.Message}",
                });
            }
        }

        _statusMessage = null;
    }

    private void RemoveFile(PendingFile file)
    {
        _pendingFiles.Remove(file);
    }

    private void ClearFiles()
    {
        _pendingFiles.Clear();
        _statusMessage = null;
    }

    private async Task HandleUpload()
    {
        var validFiles = _pendingFiles.Where(f => f.Error is null && f.Data is not null).ToList();
        if (validFiles.Count == 0)
        {
            Snackbar.Add("Nenhum arquivo válido para enviar.", Severity.Warning);
            return;
        }

        _isUploading = true;
        _uploadedCount = 0;
        _statusMessage = null;

        try
        {
            foreach (var file in validFiles)
            {
                try
                {
                    var raw = new RawInvoice
                    {
                        FileName = file.FileName,
                        ImageData = file.Data!,
                    };

                    await InvoiceService.SendInvoicesToProcessAsync(raw);
                    _uploadedCount++;
                    _pendingFiles.Remove(file);
                }
                catch (Exception ex)
                {
                    file.Error = $"Erro no envio: {ex.Message}";
                }
            }

            if (_uploadedCount > 0)
            {
                _statusMessage = $"{_uploadedCount} arquivo(s) enviado(s) para processamento com sucesso.";
                _statusIsError = false;
                Snackbar.Add(_statusMessage, Severity.Success);
            }

            if (_pendingFiles.Any(f => f.Error is not null))
            {
                _statusMessage = "Alguns arquivos apresentaram erros.";
                _statusIsError = true;
            }
        }
        finally
        {
            _isUploading = false;
        }
    }
}
