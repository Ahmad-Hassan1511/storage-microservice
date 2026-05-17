using DomainFile = Storage.Domain.Entities.File;
using Storage.Application.DTOs;

namespace Storage.Application.Abstractions;

public interface IFileRepository
{
    /// <summary>Returns the file by ID scoped to tenantId, or null if not found.</summary>
    /// <remarks>Implementations must eager-load the Permissions collection.</remarks>
    Task<DomainFile?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct);
    Task AddAsync(DomainFile file, CancellationToken ct);
    Task<IReadOnlyList<DomainFile>> ListAsync(FileListQuery query, CancellationToken ct);
    Task<bool> HardDeleteAsync(Guid id, Guid tenantId, CancellationToken ct);
}
