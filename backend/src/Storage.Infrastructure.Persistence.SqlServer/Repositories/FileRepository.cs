using DomainFile = Storage.Domain.Entities.File;
using Storage.Application.Abstractions;
using Storage.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Storage.Infrastructure.Persistence.SqlServer.Repositories;

internal sealed class FileRepository : IFileRepository
{
    private readonly StorageDbContext _db;
    public FileRepository(StorageDbContext db) => _db = db;

    public async Task<DomainFile?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct)
        => await _db.Files
            .Include(f => f.Permissions)
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ct);

    public async Task AddAsync(DomainFile file, CancellationToken ct)
        => await _db.Files.AddAsync(file, ct);

    public async Task<IReadOnlyList<DomainFile>> ListAsync(FileListQuery query, CancellationToken ct)
    {
        var q = _db.Files.Where(f => f.TenantId == query.TenantId);

        if (query.OwnerService is not null)
            q = q.Where(f => f.OwnerService == query.OwnerService);

        if (query.CategoryId is not null)
            q = q.Where(f => f.CategoryId == query.CategoryId);

        if (query.Tags is { Count: > 0 })
        {
            foreach (var (key, value) in query.Tags)
                q = q.Where(f => f.Tags.Any(t => t.Key == key && t.Value == value));
        }

        if (query.Cursor is not null)
        {
            var (cursorDate, cursorId) = DecodeCursor(query.Cursor);
            q = q.Where(f => f.CreatedAt < cursorDate
                          || (f.CreatedAt == cursorDate && f.Id.CompareTo(cursorId) < 0));
        }

        return await q
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.Id)
            .Take(query.PageSize)
            .ToListAsync(ct);
    }

    public async Task<bool> HardDeleteAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var file = await _db.Files.IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId, ct);
        if (file is null) return false;
        _db.Files.Remove(file);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static (DateTime date, Guid id) DecodeCursor(string cursor)
    {
        var bytes = Convert.FromBase64String(cursor);
        var raw = System.Text.Encoding.UTF8.GetString(bytes);
        var parts = raw.Split(':');
        var ticks = long.Parse(parts[0]);
        var id = Guid.Parse(parts[1]);
        return (new DateTime(ticks, DateTimeKind.Utc), id);
    }
}
