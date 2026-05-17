using Storage.Domain.Entities;

namespace Storage.Application.Abstractions;

public interface IAuditRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken ct);
}
