using Storage.Application.Abstractions;
using Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Storage.Infrastructure.Persistence.SqlServer.Repositories;

internal sealed class FileVersionRepository : IFileVersionRepository
{
    private readonly StorageDbContext _db;
    public FileVersionRepository(StorageDbContext db) => _db = db;

    public async Task AddAsync(FileVersion version, CancellationToken ct)
        => await _db.FileVersions.AddAsync(version, ct);

    public async Task<IReadOnlyList<FileVersion>> GetByFileIdAsync(
        Guid fileId, Guid tenantId, CancellationToken ct)
        => await _db.FileVersions
            .Where(v => v.FileId == fileId
                     && _db.Files.Any(f => f.Id == fileId && f.TenantId == tenantId))
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
}
