using DomainFile = Storage.Domain.Entities.File;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Storage.Infrastructure.Persistence.SqlServer.Interceptors;

public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        StampSoftDeletes(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampSoftDeletes(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void StampSoftDeletes(DbContext? context)
    {
        if (context is null) return;
        var now = DateTime.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries<DomainFile>()
            .Where(e => e.State == EntityState.Deleted))
        {
            var alreadySoftDeleted = entry.Property("DeletedAt").CurrentValue is not null;
            if (alreadySoftDeleted) continue;  // hard delete: skip interception

            entry.State = EntityState.Modified;
            entry.Property("DeletedAt").CurrentValue = now;
            entry.Property("UpdatedAt").CurrentValue = now;
        }
    }
}
