using invoices.api.Data.Context;
using invoices.api.Data.Repositories;
using invoices.core.Models;
using invoices.tests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace invoices.tests.Repositories;

public class InvoiceRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly InvoiceRepository _repo;
    private readonly SqliteConnection _connection;

    public InvoiceRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestAppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new TestAppDbContext(options);
        _db.Database.EnsureCreated();
        _repo = new InvoiceRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SeedInvoicesAsync()
    {
        var raw1 = new RawInvoice { Id = Guid.NewGuid(), FileName = "inv1.pdf" };
        var raw2 = new RawInvoice { Id = Guid.NewGuid(), FileName = "inv2.pdf" };
        var raw3 = new RawInvoice { Id = Guid.NewGuid(), FileName = "inv3.pdf" };
        _db.RawInvoices.AddRange(raw1, raw2, raw3);

        _db.Invoices.AddRange(
            new Invoice
            {
                Id = Guid.NewGuid(),
                RawId = raw1.Id,
                Date = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                AccessKey = "KEY001",
                Items = new List<ParsedItem> { new() { Name = "Item A", Quantity = 2, UnitPrice = 10m, Total = 20m } }
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                RawId = raw2.Id,
                Date = new DateTime(2026, 2, 10, 0, 0, 0, DateTimeKind.Utc),
                AccessKey = "KEY002",
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                RawId = raw3.Id,
                Date = new DateTime(2025, 12, 5, 0, 0, 0, DateTimeKind.Utc),
                AccessKey = "KEY003",
            }
        );
        await _db.SaveChangesAsync();
    }

    // ─── GetAllAsync ───

    [Fact]
    public async Task GetAllAsync_ShouldReturnPaginatedResults()
    {
        await SeedInvoicesAsync();
        var result = await _repo.GetAllAsync(1, 2);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnSecondPage()
    {
        await SeedInvoicesAsync();
        var result = await _repo.GetAllAsync(2, 2);
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByYear()
    {
        await SeedInvoicesAsync();
        var result = await _repo.GetAllAsync(1, 10, year: 2026);
        result.Should().HaveCount(2);
        result.Should().OnlyContain(i => i.Date!.Value.Year == 2026);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByMonth()
    {
        await SeedInvoicesAsync();
        var result = await _repo.GetAllAsync(1, 10, year: 2026, month: 1);
        result.Should().HaveCount(1);
        result[0].AccessKey.Should().Be("KEY001");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmpty_WhenNoMatch()
    {
        await SeedInvoicesAsync();
        var result = await _repo.GetAllAsync(1, 10, year: 2030);
        result.Should().BeEmpty();
    }

    // ─── GetCountAsync ───

    [Fact]
    public async Task GetCountAsync_ShouldReturnTotalCount()
    {
        await SeedInvoicesAsync();
        var count = await _repo.GetCountAsync();
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetCountAsync_ShouldFilterByYear()
    {
        await SeedInvoicesAsync();
        var count = await _repo.GetCountAsync(year: 2026);
        count.Should().Be(2);
    }

    // ─── GetByIdAsync ───

    [Fact]
    public async Task GetByIdAsync_ShouldReturnInvoice_WhenFound()
    {
        await SeedInvoicesAsync();
        var all = await _repo.GetAllAsync(1, 10);
        var first = all.First();

        var result = await _repo.GetByIdAsync(first.Id);
        result.Should().NotBeNull();
        result!.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldIncludeItems()
    {
        await SeedInvoicesAsync();
        var all = await _repo.GetAllAsync(1, 10);
        var withItems = all.First(i => (i.Items?.Count ?? 0) > 0);

        var result = await _repo.GetByIdAsync(withItems.Id);
        result!.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().Be("Item A");
    }

    // ─── AddAsync + SaveChangesAsync ───

    [Fact]
    public async Task AddAsync_ShouldPersistInvoice()
    {
        var rawInvoice = new RawInvoice { Id = Guid.NewGuid(), FileName = "new.pdf" };
        await _repo.AddRawInvoiceAsync(rawInvoice);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            RawId = rawInvoice.Id,
            AccessKey = "NEW001",
            Items = new List<ParsedItem> { new() { Name = "New Item", Quantity = 1, UnitPrice = 5m, Total = 5m } }
        };

        await _repo.AddAsync(invoice);
        await _repo.SaveChangesAsync();

        var saved = await _repo.GetByIdAsync(invoice.Id);
        saved.Should().NotBeNull();
        saved!.AccessKey.Should().Be("NEW001");
        saved.Items.Should().HaveCount(1);
    }

    // ─── DeleteAsync ───

    [Fact]
    public async Task DeleteAsync_ShouldRemoveInvoice()
    {
        await SeedInvoicesAsync();
        var all = await _repo.GetAllAsync(1, 10);
        var toDelete = await _repo.GetByIdTrackedAsync(all.First().Id);

        await _repo.DeleteAsync(toDelete!);

        var count = await _repo.GetCountAsync();
        count.Should().Be(all.Count - 1);
    }

    // ─── DeleteManyAsync ───

    [Fact]
    public async Task DeleteManyAsync_ShouldRemoveMultipleInvoices()
    {
        await SeedInvoicesAsync();
        var all = await _repo.GetAllAsync(1, 10);
        var ids = all.Take(2).Select(i => i.Id).ToList();

        await _repo.DeleteManyAsync(ids);

        var count = await _repo.GetCountAsync();
        count.Should().Be(all.Count - 2);
    }

    [Fact]
    public async Task DeleteManyAsync_ShouldDoNothing_WhenIdsEmpty()
    {
        await SeedInvoicesAsync();
        var countBefore = await _repo.GetCountAsync();

        await _repo.DeleteManyAsync(new List<Guid>());

        var countAfter = await _repo.GetCountAsync();
        countAfter.Should().Be(countBefore);
    }

    // ─── GetGroupsAsync ───

    [Fact]
    public async Task GetGroupsAsync_ShouldReturnGroupedCounts()
    {
        await SeedInvoicesAsync();
        var groups = await _repo.GetGroupsAsync();

        groups.Should().HaveCount(3);
        groups.Should().Contain(g => g.Year == 2026 && g.Month == 1 && g.Count == 1);
        groups.Should().Contain(g => g.Year == 2026 && g.Month == 2 && g.Count == 1);
        groups.Should().Contain(g => g.Year == 2025 && g.Month == 12 && g.Count == 1);
    }

    [Fact]
    public async Task GetGroupsAsync_ShouldBeOrderedByYearDescThenMonthDesc()
    {
        await SeedInvoicesAsync();
        var groups = await _repo.GetGroupsAsync();

        groups[0].Year.Should().Be(2026);
        groups[0].Month.Should().Be(2);
        groups[1].Month.Should().Be(1);
        groups[2].Year.Should().Be(2025);
    }

    // ─── GetByMonthAsync ───

    [Fact]
    public async Task GetByMonthAsync_ShouldReturnAllInvoicesForMonth()
    {
        await SeedInvoicesAsync();
        var result = await _repo.GetByMonthAsync(2026, 1);

        result.Should().HaveCount(1);
        result[0].AccessKey.Should().Be("KEY001");
    }

    [Fact]
    public async Task GetByMonthAsync_ShouldReturnEmpty_WhenNoInvoices()
    {
        var result = await _repo.GetByMonthAsync(2020, 1);
        result.Should().BeEmpty();
    }
}
