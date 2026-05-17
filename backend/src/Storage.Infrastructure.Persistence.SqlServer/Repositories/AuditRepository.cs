using Storage.Application.Abstractions;
using Storage.Domain.Entities;

namespace Storage.Infrastructure.Persistence.SqlServer.Repositories;

internal sealed class AuditRepository : IAuditRepository
{
    private readonly StorageDbContext _db;
    public AuditRepository(StorageDbContext db) => _db = db;

    public async Task AddAsync(AuditEntry entry, CancellationToken ct)
        => await _db.AuditLog.AddAsync(entry, ct);
}
