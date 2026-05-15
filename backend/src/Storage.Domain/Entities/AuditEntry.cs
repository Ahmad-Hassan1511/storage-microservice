using Storage.Domain.Common;

namespace Storage.Domain.Entities;

public class AuditEntry : EntityBase
{
    public Guid FileId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string PerformedBy { get; private set; } = string.Empty;
    public DateTime PerformedAt { get; private set; }
    public string? Details { get; private set; }
}
