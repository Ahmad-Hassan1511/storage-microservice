using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Storage.Application.Abstractions;

namespace Storage.Infrastructure.Storage.AzureBlob;

public static class AzureBlobServiceCollectionExtensions
{
    public static IServiceCollection AddAzureBlobStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AzureBlobOptions>(configuration.GetSection("Storage:AzureBlob"));
        services.AddSingleton<IFileStorageProvider, AzureBlobStorageProvider>();
        return services;
    }
}
