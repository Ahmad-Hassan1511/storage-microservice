using Storage.Application.Abstractions;
using Storage.Infrastructure.Persistence.SqlServer.Interceptors;
using Storage.Infrastructure.Persistence.SqlServer.Repositories;
using Storage.Infrastructure.Persistence.SqlServer.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Storage.Infrastructure.Persistence.SqlServer.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqlServerPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<SoftDeleteInterceptor>();

        services.AddDbContext<StorageDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });
            options.AddInterceptors(sp.GetRequiredService<SoftDeleteInterceptor>());
        });

        services.AddScoped<IFileRepository, FileRepository>();
        services.AddScoped<IFileCategoryRepository, FileCategoryRepository>();
        services.AddScoped<IFileVersionRepository, FileVersionRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<FileCategorySeeder>();

        return services;
    }
}
