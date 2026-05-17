using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Storage.Sdk;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStorageClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageClientOptions>(configuration.GetSection("StorageClient"));

        services.AddHttpClient("storage-sdk");

        services.AddTransient<IStorageClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<StorageClientOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("storage-sdk");
            http.BaseAddress = new Uri(opts.BaseUrl);
            if (!string.IsNullOrEmpty(opts.AccessToken))
                http.DefaultRequestHeaders.Authorization = new("Bearer", opts.AccessToken);
            return new StorageClient(http, opts);
        });

        return services;
    }
}
