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

    // EF Core private constructor
    private FileVersion() { }

    /// <summary>
    /// Factory for creating a new FileVersion. Accepts string storageKey and checksumSha256
    /// and wraps them in their respective value objects.
    /// </summary>
    public static FileVersion Create(
        Guid fileId,
        int versionNumber,
        string storageKey,
        string checksumSha256,
        long sizeBytes,
        DateTime createdAt)
    {
        return new FileVersion
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            VersionNumber = versionNumber,
            StorageKey = new StorageKey(storageKey),
            Checksum = new Checksum(checksumSha256),
            SizeBytes = sizeBytes,
            CreatedAt = createdAt
        };
    }
}
