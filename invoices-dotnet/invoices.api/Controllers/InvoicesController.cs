using ClosedXML.Excel;
using invoices.core.Models;
using invoices.core.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace invoices.api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController(IInvoiceService invoiceService, IInvoiceRepository invoiceRepo)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Invoice>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool ascending = false,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default
    )
    {
        return await invoiceService.GetAllAsync(page, pageSize, search, sortBy, ascending, year, month, ct);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Invoice>> GetById(Guid id, CancellationToken ct = default)
    {
        var invoice = await invoiceService.GetByIdAsync(id, ct);

        if (invoice is null)
            return NotFound();

        return invoice;
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetCount(
        [FromQuery] string? search = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default
    )
    {
        return await invoiceService.GetCountAsync(search, year, month, ct);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        try
        {
            await invoiceService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("batch-delete")]
    public async Task<IActionResult> DeleteMany([FromBody] BatchDeleteRequest request, CancellationToken ct = default)
    {
        await invoiceService.DeleteManyAsync(request.Ids, ct);
        return NoContent();
    }

    [HttpGet("groups")]
    public async Task<ActionResult<List<YearMonthGroup>>> GetGroups(CancellationToken ct = default)
    {
        return await invoiceService.GetGroupsAsync(ct);
    }

    [HttpGet("by-month")]
    public async Task<ActionResult<List<Invoice>>> GetByMonth(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        return await invoiceService.GetByMonthAsync(year, month, ct);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        Invoice invoice,
        CancellationToken ct = default
    )
    {
        if (id != invoice.Id)
            return BadRequest("Route id does not match invoice id.");

        try
        {
            await invoiceService.UpdateAsync(invoice, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/raw-image")]
    public async Task<IActionResult> GetRawImage(Guid id, CancellationToken ct = default)
    {
        var raw = await invoiceRepo.GetRawInvoiceByInvoiceIdAsync(id, ct);

        if (raw?.ImageData is null)
            return NotFound();

        var fileName = string.IsNullOrWhiteSpace(raw.FileName) ? "original.pdf" : raw.FileName;

        return File(raw.ImageData, "application/pdf", fileName);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        var invoices = await invoiceService.GetByMonthAsync(year, month, ct);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Relatório");

        ws.Cell(1, 1).Value = "Mês de referência";
        ws.Cell(1, 2).Value = $"{year:D4}-{month:D2}";
        ws.Cell(1, 3).Value = DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm");

        ws.Cell(3, 1).Value = "Estabelecimento";
        ws.Cell(3, 2).Value = "CNPJ";
        ws.Cell(3, 3).Value = "Data";
        ws.Cell(3, 4).Value = "Chave de Acesso";
        ws.Cell(3, 5).Value = "Total";
        ws.Cell(3, 6).Value = "Itens";
        ws.Cell(3, 7).Value = "Status";

        var headerRow = ws.Row(3);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#EFE7D8");

        int row = 4;
        foreach (var inv in invoices)
        {
            ws.Cell(row, 1).Value = inv.RawEstablishment ?? "";
            ws.Cell(row, 2).Value = inv.RawCnpj ?? "";
            ws.Cell(row, 3).Value = inv.Date?.ToString("dd/MM/yyyy") ?? "";
            ws.Cell(row, 4).Value = inv.AccessKey ?? "";
            ws.Cell(row, 5).Value = inv.Total;
            ws.Cell(row, 5).Style.NumberFormat.Format = "R$ #,##0.00";
            ws.Cell(row, 6).Value = inv.Items?.Count ?? 0;
            ws.Cell(row, 7).Value = inv.IsValid ? "Válida" : "Inválida";
            row++;
        }

        ws.Cell(row, 1).Value = "Total";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 5).FormulaA1 = $"=SUM(E4:E{row - 1})";
        ws.Cell(row, 5).Style.Font.Bold = true;
        ws.Cell(row, 5).Style.NumberFormat.Format = "R$ #,##0.00";

        ws.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"relatorio-{year:D4}-{month:D2}.xlsx");
    }

    [HttpPost("process")]
    public async Task<IActionResult> SendToProcess(RawInvoice raw, CancellationToken ct = default)
    {
        await invoiceService.SendInvoicesToProcessAsync(raw, ct);
        return Accepted();
    }
}
