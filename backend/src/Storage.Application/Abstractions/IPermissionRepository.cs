using Storage.Domain.Entities;

namespace Storage.Application.Abstractions;

public interface IPermissionRepository
{
    Task<FilePermission?> GetAsync(Guid fileId, string principalType, string principalId, CancellationToken ct);
    Task AddAsync(FilePermission permission, CancellationToken ct);
    Task RemoveAsync(Guid fileId, string principalType, string principalId, CancellationToken ct);
}
