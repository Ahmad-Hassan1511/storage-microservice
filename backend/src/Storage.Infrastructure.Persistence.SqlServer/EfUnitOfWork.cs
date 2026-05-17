using Storage.Application.Abstractions;
using Storage.Infrastructure.Persistence.SqlServer.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Storage.Infrastructure.Persistence.SqlServer;

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly StorageDbContext _db;

    public EfUnitOfWork(StorageDbContext db)
    {
        _db = db;
        Files = new FileRepository(db);
        Categories = new FileCategoryRepository(db);
        FileVersions = new FileVersionRepository(db);
        Permissions = new PermissionRepository(db);
        Audit = new AuditRepository(db);
    }

    public IFileRepository Files { get; }
    public IFileCategoryRepository Categories { get; }
    public IFileVersionRepository FileVersions { get; }
    public IPermissionRepository Permissions { get; }
    public IAuditRepository Audit { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);

    public Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await work();
            await tx.CommitAsync(ct);
        });
    }
}
