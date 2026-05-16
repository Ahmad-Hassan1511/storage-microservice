using Storage.Application.Abstractions;
using Storage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Storage.Infrastructure.Persistence.SqlServer.Repositories;

internal sealed class FileCategoryRepository : IFileCategoryRepository
{
    private readonly StorageDbContext _db;
    public FileCategoryRepository(StorageDbContext db) => _db = db;

    public async Task<FileCategory?> GetByIdAsync(string categoryId, CancellationToken ct)
        => await _db.FileCategories.FindAsync([categoryId], ct);

    public async Task<IReadOnlyList<FileCategory>> ListAllAsync(CancellationToken ct)
        => await _db.FileCategories.OrderBy(c => c.DisplayName).ToListAsync(ct);
}
