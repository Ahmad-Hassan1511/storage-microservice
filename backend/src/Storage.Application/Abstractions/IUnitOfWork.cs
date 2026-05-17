namespace Storage.Application.Abstractions;

public interface IUnitOfWork
{
    IFileRepository Files { get; }
    IFileCategoryRepository Categories { get; }
    IFileVersionRepository FileVersions { get; }
    IPermissionRepository Permissions { get; }
    IAuditRepository Audit { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct);
}
