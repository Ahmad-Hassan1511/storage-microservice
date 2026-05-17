using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Documents;

// Consumed when Storage publishes a file.ready integration event
public sealed record FileReadyIntegrationEvent(
    Guid FileId,
    Guid TenantId,
    string OwnerService,
    string? DownloadUrl,
    Guid CorrelationId,
    DateTime OccurredAt,
    string Source);

public sealed class FileReadyHandler(DocumentsDbContext db) : IConsumer<FileReadyIntegrationEvent>
{
    public async Task Consume(ConsumeContext<FileReadyIntegrationEvent> context)
    {
        var evt = context.Message;
        if (!evt.OwnerService.Equals("documents-service", StringComparison.OrdinalIgnoreCase))
            return;

        var doc = await db.Documents
            .FirstOrDefaultAsync(d => d.FileId == evt.FileId, context.CancellationToken);

        if (doc is null) return;

        doc.Status = "ready";
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
