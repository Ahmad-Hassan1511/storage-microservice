using DomainFile = Storage.Domain.Entities.File;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storage.Infrastructure.Persistence.SqlServer.Repositories;
using Storage.Infrastructure.Persistence.SqlServer.Tests.Infrastructure;

namespace Storage.Infrastructure.Persistence.SqlServer.Tests;

public class SoftDeleteTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public SoftDeleteTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SoftDeletedFilesExcluded()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext();

        var file = DomainFile.Create(tenantId, "svc", "image", "photo.jpg", "image/jpeg", 512);
        await db.Files.AddAsync(file, ct);
        await db.SaveChangesAsync(ct);

        // Remove triggers SoftDeleteInterceptor which stamps DeletedAt
        db.Files.Remove(file);
        await db.SaveChangesAsync(ct);

        // Query filter (DeletedAt == null) must hide the soft-deleted file
        await using var db2 = _fixture.CreateDbContext();
        var found = await db2.Files.FirstOrDefaultAsync(f => f.Id == file.Id, ct);
        found.Should().BeNull("soft-deleted file must be invisible to default queries");
    }

    [Fact]
    public async Task HardDelete_FindsSoftDeleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext();

        var file = DomainFile.Create(tenantId, "svc", "image", "remove.jpg", "image/jpeg", 256);
        await db.Files.AddAsync(file, ct);
        await db.SaveChangesAsync(ct);

        // Soft-delete via Remove → interceptor stamps DeletedAt
        db.Files.Remove(file);
        await db.SaveChangesAsync(ct);

        // HardDeleteAsync must find the soft-deleted row and physically remove it
        await using var db2 = _fixture.CreateDbContext();
        var repo = new FileRepository(db2);
        var result = await repo.HardDeleteAsync(file.Id, tenantId, ct);

        result.Should().BeTrue("hard delete must find and remove the soft-deleted row");

        await using var db3 = _fixture.CreateDbContext();
        var gone = await db3.Files.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == file.Id, ct);
        gone.Should().BeNull("file must be physically removed after HardDeleteAsync");
    }
}
