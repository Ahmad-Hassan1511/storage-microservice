using Storage.Domain.Common;
using Storage.Domain.Enums;
using Storage.Domain.Events;
using Storage.Domain.ValueObjects;

namespace Storage.Domain.Entities;

/// <summary>
/// File aggregate root. Note: do not add 'using System.IO' in consumer files that use this type.
/// Use 'using DomainFile = Storage.Domain.Entities.File' alias in consumer projects to avoid
/// collision with System.IO.File.
/// </summary>
public class File : EntityBase
{
    private FileStatus _status;

    public FileStatus Status => _status;

    public Guid TenantId { get; private set; }
    public string OwnerService { get; private set; } = string.Empty;
    public string CategoryId { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public StorageKey? StorageKey { get; private set; }
    public Checksum? Checksum { get; private set; }
    public Visibility Visibility { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Nullable self-references for preview/thumbnail generated files
    public Guid? PreviewFileId { get; private set; }
    public Guid? ThumbnailFileId { get; private set; }

    private readonly List<FileVersion> _versions = [];
    private readonly List<FilePermission> _permissions = [];
    private readonly List<FileTag> _tags = [];
    private readonly List<AuditEntry> _auditEntries = [];

    public IReadOnlyList<FileVersion> Versions => _versions.AsReadOnly();
    public IReadOnlyList<FilePermission> Permissions => _permissions.AsReadOnly();
    public IReadOnlyList<FileTag> Tags => _tags.AsReadOnly();
    public IReadOnlyList<AuditEntry> AuditEntries => _auditEntries.AsReadOnly();

    // Private constructor — use factory
    private File() { }

    public void Transition(FileStatus newStatus)
    {
        var valid = (_status, newStatus) switch
        {
            (FileStatus.Pending,    FileStatus.Scanning)    => true,
            (FileStatus.Scanning,   FileStatus.Ready)        => true,
            (FileStatus.Scanning,   FileStatus.Quarantined)  => true,
            (FileStatus.Ready,      FileStatus.Deleted)      => true,
            _ => false
        };

        if (!valid)
            throw new InvalidStatusTransitionException(_status, newStatus);

        _status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetOriginalFileName(string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("OriginalFileName cannot be blank.", nameof(originalFileName));
        OriginalFileName = originalFileName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetVisibility(Visibility visibility)
    {
        Visibility = visibility;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateTags(Dictionary<string, string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _tags.Clear();
        foreach (var (key, value) in tags)
            _tags.Add(new FileTag(key, value));
        UpdatedAt = DateTime.UtcNow;
    }

    public static File Create(
        Guid tenantId,
        string ownerService,
        string categoryId,
        string originalFileName,
        string mimeType,
        long sizeBytes)
    {
        var now = DateTime.UtcNow;
        var file = new File
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OwnerService = ownerService,
            CategoryId = categoryId,
            OriginalFileName = originalFileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            _status = FileStatus.Pending,
            Visibility = Visibility.Private,
            CreatedAt = now,
            UpdatedAt = now
        };

        file.RaiseDomainEvent(new FileCreatedEvent(
            file.Id,
            tenantId,
            ownerService,
            categoryId,
            Guid.NewGuid(),
            now));

        return file;
    }

    /// <summary>
    /// Rehydration factory for persistence adapters and test fixtures.
    /// Creates a File in any status with StorageKey and Permissions pre-populated.
    /// </summary>
    public static File Rehydrate(
        Guid id,
        Guid tenantId,
        string ownerService,
        string categoryId,
        string originalFileName,
        string mimeType,
        long sizeBytes,
        FileStatus status,
        StorageKey storageKey,
        Visibility visibility,
        DateTime createdAt,
        IEnumerable<FilePermission>? permissions = null)
    {
        var file = new File
        {
            Id = id,
            TenantId = tenantId,
            OwnerService = ownerService,
            CategoryId = categoryId,
            OriginalFileName = originalFileName,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            _status = status,
            StorageKey = storageKey,
            Visibility = visibility,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        if (permissions is not null)
            file._permissions.AddRange(permissions);

        return file;
    }
}
