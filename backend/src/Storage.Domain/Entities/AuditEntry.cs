using Storage.Domain.Common;

namespace Storage.Domain.Entities;

public class AuditEntry : EntityBase
{
    public Guid FileId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string PerformedBy { get; private set; } = string.Empty;
    public DateTime PerformedAt { get; private set; }
    public string? Details { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Create(Guid fileId, string action, string performedBy, DateTime performedAt, string? details = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            Action = action,
            PerformedBy = performedBy,
            PerformedAt = performedAt,
            Details = details
        };
}
