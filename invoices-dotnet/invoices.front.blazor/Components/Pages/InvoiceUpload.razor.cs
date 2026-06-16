using invoices.core.Models;
using invoices.front.blazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;

namespace invoices.front.blazor.Components.Pages;

public partial class InvoiceUpload : ComponentBase
{
    [Inject]
    private InvoiceService InvoiceService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

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
        public bool IsUploading { get; set; }
        public bool IsDone { get; set; }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    private async Task HandleFileSelected(InputFileChangeEventArgs args)
    {
        var files = args.GetMultipleFiles(maximumFileCount: 50);
        var addedCount = 0;

        foreach (var file in files)
        {
            if (_pendingFiles.Any(f => f.FileName == file.Name && f.Error is null))
                continue;

            try
            {
                if (!file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    Snackbar.Add($"Ignorado (não é PDF): {file.Name}", Severity.Warning);
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
                addedCount++;
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
        if (addedCount > 0)
            Snackbar.Add($"{addedCount} arquivo(s) adicionado(s).", Severity.Info);
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
        var validFiles = _pendingFiles.Where(f => f.Error is null && f.Data is not null && !f.IsDone).ToList();
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
                file.IsUploading = true;
                StateHasChanged();

                try
                {
                    var raw = new RawInvoice
                    {
                        FileName = file.FileName,
                        ImageData = file.Data!,
                    };

                    await InvoiceService.SendInvoicesToProcessAsync(raw);
                    file.IsUploading = false;
                    file.IsDone = true;
                    _uploadedCount++;
                }
                catch (Exception ex)
                {
                    file.IsUploading = false;
                    file.Error = $"Erro no envio: {ex.Message}";
                }

                StateHasChanged();
            }

            if (_uploadedCount > 0)
            {
                _statusMessage = $"{_uploadedCount} arquivo(s) enviado(s) para processamento com sucesso.";
                _statusIsError = false;
                Snackbar.Add(_statusMessage, Severity.Success);
            }

            var errorCount = _pendingFiles.Count(f => f.Error is not null);
            if (errorCount > 0)
            {
                Snackbar.Add($"{errorCount} arquivo(s) com erro. Verifique a lista.", Severity.Error);
            }
        }
        finally
        {
            _isUploading = false;
        }
    }
}
