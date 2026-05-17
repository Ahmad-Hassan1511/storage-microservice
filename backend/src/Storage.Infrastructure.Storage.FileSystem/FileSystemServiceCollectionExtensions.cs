using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Storage.FileSystem;

public static class FileSystemServiceCollectionExtensions
{
    public static IServiceCollection AddFileSystemStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FileSystemOptions>(configuration.GetSection("Storage:FileSystem"));
        services.AddSingleton<IFileStorageProvider, FileSystemStorageProvider>();
        return services;
    }
}
