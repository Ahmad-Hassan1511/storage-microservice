using Storage.Domain.Entities;

namespace Storage.Application.Abstractions;

public interface IFileVersionRepository
{
    Task AddAsync(FileVersion version, CancellationToken ct);
    Task<IReadOnlyList<FileVersion>> GetByFileIdAsync(Guid fileId, Guid tenantId, CancellationToken ct);
}
