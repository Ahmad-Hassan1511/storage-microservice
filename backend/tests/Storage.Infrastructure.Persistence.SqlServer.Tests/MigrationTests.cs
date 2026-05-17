using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storage.Infrastructure.Persistence.SqlServer.Seeders;
using Storage.Infrastructure.Persistence.SqlServer.Tests.Infrastructure;

namespace Storage.Infrastructure.Persistence.SqlServer.Tests;

public class MigrationTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;
    public MigrationTests(SqlServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SchemaCreatedCleanly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _fixture.CreateDbContext();

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name FROM sys.tables WHERE type = 'U'")
            .ToListAsync(ct);

        tables.Should().Contain(["Files", "FileCategories", "FileVersions",
            "FilePermissions", "FileTags", "AuditLog"]);

        var constraints = await db.Database
            .SqlQueryRaw<string>("SELECT name FROM sys.check_constraints")
            .ToListAsync(ct);

        constraints.Should().Contain("CK_Files_Status");
        constraints.Should().Contain("CK_FilePermissions_PrincipalType");

        var indexes = await db.Database
            .SqlQueryRaw<string>(
                "SELECT i.name FROM sys.indexes i " +
                "JOIN sys.tables t ON i.object_id = t.object_id " +
                "WHERE t.name = 'Files' AND i.type = 2")
            .ToListAsync(ct);

        indexes.Should().NotBeEmpty("at least one non-clustered index should exist on Files");
    }

    [Fact]
    public async Task SeedIsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _fixture.CreateDbContext();

        db.FileCategories.RemoveRange(db.FileCategories);
        await db.SaveChangesAsync(ct);

        var seeder1 = new FileCategorySeeder(db);
        await seeder1.SeedAsync(ct);
        var countAfterFirstRun = await db.FileCategories.CountAsync(ct);

        var seeder2 = new FileCategorySeeder(db);
        await seeder2.SeedAsync(ct);
        var countAfterSecondRun = await db.FileCategories.CountAsync(ct);

        countAfterFirstRun.Should().Be(7, "seeder must insert exactly 7 categories");
        countAfterSecondRun.Should().Be(7, "second run must be a no-op");
    }
}
