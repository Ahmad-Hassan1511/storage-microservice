using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Storage.Infrastructure.Cache.InMemory;
using Storage.Infrastructure.Cache.Redis;
using Storage.Infrastructure.Messaging.AzureServiceBus;
using Storage.Infrastructure.Messaging.RabbitMQ;
using Storage.Infrastructure.Persistence.SqlServer;
using Storage.Infrastructure.Persistence.SqlServer.Extensions;
using Storage.Infrastructure.Persistence.SqlServer.Seeders;
using Storage.Infrastructure.Storage.AzureBlob;
using Storage.Infrastructure.Storage.FileSystem;
using Storage.Infrastructure.Storage.Wasabi;
using Storage.Application.Services;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();
if (builder.Environment.IsDevelopment())
    builder.Services.AddOpenApi("dev");

// Controllers
builder.Services.AddControllers();

// Auth
var authAuthority = builder.Configuration["Auth:Authority"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authAuthority;
        options.Audience = builder.Configuration["Auth:Audience"] ?? "storage-api";
        options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("Auth:RequireHttpsMetadata");

        // Dev/test mode: when no Authority is configured, validate with a fixed HS256 dev key
        // so E2E tests can generate tokens without Keycloak running.
        if (string.IsNullOrEmpty(authAuthority))
        {
            var devKey = builder.Configuration["Auth:DevelopmentSigningKey"] ?? "dev-signing-key-32-bytes-minimum!";
            var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(devKey));
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
            };
        }
    });
builder.Services.AddAuthorization();

// Application services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<UploadService>();
builder.Services.AddScoped<DownloadService>();
builder.Services.AddScoped<FileManagementService>();

// Persistence
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost,1433;Database=StorageDb;User Id=sa;Password=Dev@123456;TrustServerCertificate=true";
builder.Services.AddSqlServerPersistence(connStr);

// Storage adapter (config key: Storage:Provider = wasabi | azureblob | filesystem)
switch ((builder.Configuration["Storage:Provider"] ?? "filesystem").ToLowerInvariant())
{
    case "wasabi":
        builder.Services.AddWasabiStorage(builder.Configuration);
        break;
    case "azureblob":
        builder.Services.AddAzureBlobStorage(builder.Configuration);
        break;
    default:
        builder.Services.AddFileSystemStorage(builder.Configuration);
        break;
}

// Cache adapter (config key: Cache:Provider = redis | inmemory)
if ((builder.Configuration["Cache:Provider"] ?? "inmemory").Equals("redis", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddRedisCacheProvider(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
else
    builder.Services.AddInMemoryCacheProvider();

// Messaging adapter (config key: Messaging:Provider = rabbitmq | azureservicebus)
if ((builder.Configuration["Messaging:Provider"] ?? "rabbitmq").Equals("azureservicebus", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddAzureServiceBusMessaging(builder.Configuration.GetConnectionString("AzureServiceBus") ?? string.Empty);
else
    builder.Services.AddRabbitMqMessaging(builder.Configuration);

var app = builder.Build();

// Apply EF Core migrations and seed on startup (non-fatal — DB may not be available in dev without Docker)
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<StorageDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<FileCategorySeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Migration/seeder skipped — database unavailable at startup.");
}

app.MapOpenApi();
app.MapScalarApiReference("/swagger");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
