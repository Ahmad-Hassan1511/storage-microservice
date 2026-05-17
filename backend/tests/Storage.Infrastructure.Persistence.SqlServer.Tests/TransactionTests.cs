using DomainFile = Storage.Domain.Entities.File;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storage.Infrastructure.Persistence.SqlServer.Tests.Infrastructure;

namespace Storage.Infrastructure.Persistence.SqlServer.Tests;

public class TransactionTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public TransactionTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ExecuteInTransaction_RollsBackOnFailure()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext();
        var uow = new EfUnitOfWork(db);

        var file1 = DomainFile.Create(tenantId, "svc", "document", "f1.pdf", "application/pdf", 100);
        var file2 = DomainFile.Create(tenantId, "svc", "document", "f2.pdf", "application/pdf", 200);

        var act = async () => await uow.ExecuteInTransactionAsync(async () =>
        {
            await uow.Files.AddAsync(file1, ct);
            await uow.Files.AddAsync(file2, ct);
            await uow.SaveChangesAsync(ct);
            throw new InvalidOperationException("Simulated failure");
        }, ct);

        await act.Should().ThrowAsync<InvalidOperationException>();

        await using var verifyDb = _fixture.CreateDbContext();
        var foundCount = await verifyDb.Files.IgnoreQueryFilters()
            .CountAsync(f => f.Id == file1.Id || f.Id == file2.Id, ct);

        foundCount.Should().Be(0, "transaction must have rolled back both inserts");
    }
}
