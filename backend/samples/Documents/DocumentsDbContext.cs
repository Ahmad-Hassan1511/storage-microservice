using Microsoft.EntityFrameworkCore;

namespace Documents;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
}

public sealed class Document
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? FileId { get; set; }
    public string Status { get; set; } = "uploading";
    public string? MimeType { get; set; }
    public long? SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record InitiateDocumentUploadRequest(string FileName, string MimeType, long SizeBytes);
public sealed record CompleteDocumentUploadRequest(string ChecksumSha256, long SizeBytes);
