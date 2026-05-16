using Microsoft.EntityFrameworkCore;

namespace Documents;

public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
}

public sealed class Document
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? FileId { get; set; }
    public string Status { get; set; } = "uploading"; // uploading | ready
    public string? DownloadUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record UploadDocumentRequest(
    string Title,
    string FileName,
    string MimeType,
    long SizeBytes);
