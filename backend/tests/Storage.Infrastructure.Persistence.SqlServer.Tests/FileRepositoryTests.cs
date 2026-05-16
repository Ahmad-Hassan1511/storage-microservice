using DomainFile = Storage.Domain.Entities.File;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storage.Application.DTOs;
using Storage.Domain.Entities;
using Storage.Domain.Enums;
using Storage.Infrastructure.Persistence.SqlServer.Repositories;
using Storage.Infrastructure.Persistence.SqlServer.Tests.Infrastructure;

namespace Storage.Infrastructure.Persistence.SqlServer.Tests;

public class FileRepositoryTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public FileRepositoryTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetById_IncludesPermissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext();

        var file = DomainFile.Create(tenantId, "test-svc", "document", "test.pdf", "application/pdf", 1024);
        var permission = FilePermission.Create(file.Id, "service", "svc-abc", Permission.Read);
        await db.Files.AddAsync(file, ct);
        await db.FilePermissions.AddAsync(permission, ct);
        await db.SaveChangesAsync(ct);

        await using var db2 = _fixture.CreateDbContext();
        var loaded = await db2.Files
            .Include(f => f.Permissions)
            .FirstOrDefaultAsync(f => f.Id == file.Id && f.TenantId == tenantId, ct);

        loaded.Should().NotBeNull();
        loaded!.Permissions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListAsync_CursorPagination()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        await using var db = _fixture.CreateDbContext();

        var files = Enumerable.Range(0, 5)
            .Select(_ => DomainFile.Create(tenantId, "test-svc", "document", "page.pdf", "application/pdf", 512))
            .ToList();
        await db.Files.AddRangeAsync(files, ct);
        await db.SaveChangesAsync(ct);

        await using var db2 = _fixture.CreateDbContext();
        var repo = new FileRepository(db2);

        var page1 = await repo.ListAsync(
            new FileListQuery(tenantId, null, null, null, 3, null),
            ct);
        page1.Should().HaveCount(3);

        var lastItem = page1.Last();
        var cursorRaw = $"{lastItem.CreatedAt.Ticks}:{lastItem.Id}";
        var cursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cursorRaw));

        var page2 = await repo.ListAsync(
            new FileListQuery(tenantId, null, null, cursor, 3, null),
            ct);
        page2.Should().HaveCount(2);

        var allIds = page1.Select(f => f.Id).Concat(page2.Select(f => f.Id)).ToList();
        allIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ConcurrencyToken_ThrowsOnConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        await using var db1 = _fixture.CreateDbContext();
        var file = DomainFile.Create(tenantId, "test-svc", "document", "conflict.pdf", "application/pdf", 2048);
        await db1.Files.AddAsync(file, ct);
        await db1.SaveChangesAsync(ct);

        await using var dbA = _fixture.CreateDbContext();
        await using var dbB = _fixture.CreateDbContext();

        var fileA = await dbA.Files.FirstAsync(f => f.Id == file.Id, ct);
        var fileB = await dbB.Files.FirstAsync(f => f.Id == file.Id, ct);

        fileA.Transition(FileStatus.Scanning);
        await dbA.SaveChangesAsync(ct);

        fileB.Transition(FileStatus.Scanning);
        var act = async () => await dbB.SaveChangesAsync(ct);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
