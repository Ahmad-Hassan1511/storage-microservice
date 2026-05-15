using Storage.Domain.Common;
using Storage.Domain.ValueObjects;

namespace Storage.Domain.Entities;

public class FileVersion : EntityBase
{
    public Guid FileId { get; private set; }
    public int VersionNumber { get; private set; }
    public StorageKey StorageKey { get; private set; } = null!;
    public Checksum Checksum { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
