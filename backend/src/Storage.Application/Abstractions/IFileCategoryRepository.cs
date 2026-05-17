using Storage.Domain.Entities;

namespace Storage.Application.Abstractions;

public interface IFileCategoryRepository
{
    Task<FileCategory?> GetByIdAsync(string categoryId, CancellationToken ct);
    Task<IReadOnlyList<FileCategory>> ListAllAsync(CancellationToken ct);
}
