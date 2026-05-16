using Storage.Application.Abstractions;
using Storage.Domain.Entities;
using Storage.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Storage.Infrastructure.Persistence.SqlServer.Repositories;

internal sealed class PermissionRepository : IPermissionRepository
{
    private readonly StorageDbContext _db;
    public PermissionRepository(StorageDbContext db) => _db = db;

    public async Task<FilePermission?> GetAsync(
        Guid fileId, string principalType, string principalId, CancellationToken ct)
        => await _db.FilePermissions
            .FirstOrDefaultAsync(p => p.FileId == fileId
                                   && p.PrincipalType == principalType
                                   && p.PrincipalId == principalId, ct);

    public async Task AddAsync(FilePermission permission, CancellationToken ct)
        => await _db.FilePermissions.AddAsync(permission, ct);

    public async Task RemoveAsync(
        Guid fileId, string principalType, string principalId, CancellationToken ct)
    {
        var perm = await _db.FilePermissions
            .FirstOrDefaultAsync(p => p.FileId == fileId
                                   && p.PrincipalType == principalType
                                   && p.PrincipalId == principalId, ct);
        if (perm is not null)
            _db.FilePermissions.Remove(perm);
    }
}
