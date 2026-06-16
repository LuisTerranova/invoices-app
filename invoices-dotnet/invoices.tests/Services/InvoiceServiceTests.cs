using System.Text.Json;
using System.Text.Json.Serialization;
using invoices.api.Data.Context;
using invoices.api.Services.Implementations;
using invoices.core.Models;
using invoices.core.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;

namespace invoices.tests.Services;

public class InvoiceServiceTests
{
    private readonly Mock<IInvoiceRepository> _mockRepo;
    private readonly Mock<IChannel> _mockChannel;
    private readonly Mock<ILogger<InvoiceService>> _mockLogger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly InvoiceService _sut;
    private readonly AppDbContext _db;

    public InvoiceServiceTests()
    {
        _mockRepo = new Mock<IInvoiceRepository>(MockBehavior.Strict);
        _mockChannel = new Mock<IChannel>(MockBehavior.Loose);
        _mockLogger = new Mock<ILogger<InvoiceService>>();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _sut = new InvoiceService(_mockRepo.Object, _db, _mockChannel.Object, _jsonOptions, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnListFromRepository()
    {
        var invoices = new List<Invoice>
        {
            new() { Id = Guid.NewGuid(), Establishment = new Establishment { Cnpj = "123" } },
            new() { Id = Guid.NewGuid(), Establishment = new Establishment { Cnpj = "456" } },
        };

        _mockRepo
            .Setup(r => r.GetAllAsync(1, 10, null, null, false, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

        var result = await _sut.GetAllAsync(1, 10);

        result.Should().BeSameAs(invoices);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenRepositoryReturnsEmpty()
    {
        _mockRepo
            .Setup(r => r.GetAllAsync(1, 10, null, null, false, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetAllAsync(1, 10);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnInvoice_WhenFound()
    {
        var id = Guid.NewGuid();
        var invoice = new Invoice { Id = id };

        _mockRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var result = await _sut.GetByIdAsync(id);

        result.Should().BeSameAs(invoice);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        var id = Guid.NewGuid();

        _mockRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var result = await _sut.GetByIdAsync(id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCountAsync_ShouldReturnCountFromRepository()
    {
        _mockRepo
            .Setup(r => r.GetCountAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await _sut.GetCountAsync();

        result.Should().Be(42);
    }

    [Fact]
    public async Task GetCountAsync_ShouldReturnFilteredCount_WhenSearchProvided()
    {
        _mockRepo
            .Setup(r => r.GetCountAsync("ACME", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _sut.GetCountAsync("ACME");

        result.Should().Be(5);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDelete_WhenInvoiceExists()
    {
        var id = Guid.NewGuid();
        var invoice = new Invoice { Id = id };

        _mockRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        _mockRepo
            .Setup(r => r.DeleteAsync(invoice, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.DeleteAsync(id);

        _mockRepo.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.DeleteAsync(invoice, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrow_WhenInvoiceNotFound()
    {
        var id = Guid.NewGuid();

        _mockRepo
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var act = () => _sut.DeleteAsync(id);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{id}*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdate_WhenInvoiceExists()
    {
        var existingInvoice = new Invoice { Id = Guid.NewGuid(), Items = new List<ParsedItem>() };
        var incomingInvoice = new Invoice { Id = existingInvoice.Id, Items = new List<ParsedItem> { new ParsedItem { Name = "Item 1" } } };

        _mockRepo
            .Setup(r => r.GetByIdTrackedAsync(incomingInvoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingInvoice);

        _mockRepo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.UpdateAsync(incomingInvoice);

        _mockRepo.Verify(r => r.GetByIdTrackedAsync(incomingInvoice.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenInvoiceNotFound()
    {
        var invoice = new Invoice { Id = Guid.NewGuid() };

        _mockRepo
            .Setup(r => r.GetByIdTrackedAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Invoice?)null);

        var act = () => _sut.UpdateAsync(invoice);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage($"*{invoice.Id}*");
    }

    [Fact]
    public async Task SendInvoicesToProcessAsync_ShouldSaveAndPublish()
    {
        var raw = new RawInvoice
        {
            FileName = "invoice.pdf",
            ImageData = "fake-image-data"u8.ToArray(),
        };

        _mockRepo
            .Setup(r => r.AddRawInvoiceAsync(raw, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockRepo
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var queueOk = new QueueDeclareOk("invoices_to_process", 0, 0);

        _mockChannel
            .Setup(c => c.QueueDeclareAsync(
                "invoices_to_process", true, false, false,
                It.IsAny<IDictionary<string, object?>>(), false, false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(queueOk);

        await _sut.SendInvoicesToProcessAsync(raw);

        _mockRepo.Verify(r => r.AddRawInvoiceAsync(raw, It.IsAny<CancellationToken>()), Times.Once);
        _mockRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockChannel.Verify(c => c.QueueDeclareAsync(
            "invoices_to_process", true, false, false,
            It.IsAny<IDictionary<string, object?>>(), false, false,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
