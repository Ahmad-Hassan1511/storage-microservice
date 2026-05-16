using Documents;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Storage.Sdk;

var builder = WebApplication.CreateBuilder(args);

// In-memory EF Core for demo purposes
builder.Services.AddDbContext<DocumentsDbContext>(o => o.UseInMemoryDatabase("documents"));

// Storage SDK
builder.Services.AddStorageClient(builder.Configuration);

// MassTransit consumer for file.ready events
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<FileReadyHandler>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"] ?? "amqp://guest:guest@localhost:5672");
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();

// POST /api/documents — upload a document via Storage SDK
app.MapPost("/api/documents", async (
    UploadDocumentRequest req,
    DocumentsDbContext db,
    IStorageClient storageClient,
    CancellationToken ct) =>
{
    var doc = new Document
    {
        Id = Guid.NewGuid(),
        Title = req.Title,
        Status = "uploading",
        CreatedAt = DateTime.UtcNow,
    };
    db.Documents.Add(doc);
    await db.SaveChangesAsync(ct);

    // Delegate actual file upload to the caller (return upload URL)
    var uploadRequest = new UploadFileRequest(
        CategoryId: "document",
        OriginalFileName: req.FileName,
        MimeType: req.MimeType,
        SizeBytes: req.SizeBytes,
        OwnerService: "documents-service");

    // Initiate upload only — content will be uploaded by the frontend
    return Results.Created($"/api/documents/{doc.Id}", new
    {
        documentId = doc.Id,
        status = doc.Status,
    });
});

// GET /api/documents/{id}
app.MapGet("/api/documents/{id:guid}", async (
    Guid id,
    DocumentsDbContext db,
    CancellationToken ct) =>
{
    var doc = await db.Documents.FindAsync([id], ct);
    return doc is null ? Results.NotFound() : Results.Ok(doc);
});

app.Run();
