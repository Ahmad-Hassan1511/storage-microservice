using Storage.Application.Abstractions;
using Storage.Application.Common;
using Storage.Application.DTOs;
using Storage.Application.Services;
using Storage.Infrastructure.Cache.InMemory;
using Storage.Infrastructure.Cache.Redis;
using Storage.Infrastructure.Messaging.AzureServiceBus;
using Storage.Infrastructure.Messaging.RabbitMQ;
using Storage.Infrastructure.Persistence.SqlServer.Extensions;
using Storage.Infrastructure.Persistence.SqlServer.Seeders;
using Storage.Infrastructure.Storage.AzureBlob;
using Storage.Infrastructure.Storage.FileSystem;
using Storage.Infrastructure.Storage.Wasabi;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI
builder.Services.AddOpenApi();

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
    ?? "Server=localhost,1433;Database=StorageDb;User Id=sa;Password=Storage@2024!;TrustServerCertificate=true";
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

// Seed file categories on startup (non-fatal — DB may not be available in dev without Docker)
try
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<FileCategorySeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Seeder skipped — database unavailable at startup.");
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

// ─── Helpers ─────────────────────────────────────────────────────────────────

static CallerContext BuildCaller(HttpContext ctx)
{
    var user = ctx.User;
    Guid.TryParse(user.FindFirst("tid")?.Value, out var tenantId);
    var principalId = user.FindFirst("sub")?.Value ?? user.FindFirst("oid")?.Value ?? string.Empty;
    var principalType = user.FindFirst("azp") is not null ? "service" : "user";
    var scopes = (user.FindFirst("scp")?.Value ?? user.FindFirst("roles")?.Value ?? string.Empty)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return new CallerContext(tenantId, principalType, principalId, scopes);
}

static IResult MapError(ApplicationError error) => error switch
{
    NotFoundError e            => Results.NotFound(new { error = e.Message }),
    AccessDeniedError          => Results.StatusCode(403),
    IdempotencyConflictError e => Results.Conflict(new { error = e.Message }),
    ChecksumMismatchError e    => Results.UnprocessableEntity(new { error = e.Message }),
    PolicyViolationError e     => Results.Problem(e.Message, statusCode: e.HttpStatusHint),
    _                          => Results.Problem("An unexpected error occurred.", statusCode: 500),
};

// ─── Health ───────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "storage-api" }))
   .AllowAnonymous();

// ─── POST /v1/files ──────────────────────────────────────────────────────────
app.MapPost("/v1/files", async (
    InitiateUploadRequest body,
    HttpContext ctx,
    UploadService svc,
    CancellationToken ct) =>
{
    var idempotencyKey = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? body.IdempotencyKey;
    var caller = BuildCaller(ctx);
    var result = await svc.InitiateUploadAsync(body with { IdempotencyKey = idempotencyKey }, caller, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error!);
}).RequireAuthorization();

// ─── POST /v1/files/{id}/complete ────────────────────────────────────────────
app.MapPost("/v1/files/{id:guid}/complete", async (
    Guid id,
    CompleteUploadRequest body,
    HttpContext ctx,
    UploadService svc,
    CancellationToken ct) =>
{
    var caller = BuildCaller(ctx);
    var result = await svc.CompleteUploadAsync(id, body, caller, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error!);
}).RequireAuthorization();

// ─── GET /v1/files/{id} ──────────────────────────────────────────────────────
app.MapGet("/v1/files/{id:guid}", async (
    Guid id,
    HttpContext ctx,
    DownloadService svc,
    CancellationToken ct) =>
{
    var caller = BuildCaller(ctx);
    var result = await svc.GetFileAsync(id, caller, ct);
    return result.IsSuccess ? Results.Ok(result.Value) : MapError(result.Error!);
}).RequireAuthorization();

// ─── GET /v1/files/{id}/content (audited proxy download) ─────────────────────
app.MapGet("/v1/files/{id:guid}/content", async (
    Guid id,
    HttpContext ctx,
    DownloadService svc,
    CancellationToken ct) =>
{
    var caller = BuildCaller(ctx);
    var result = await svc.GetFileStreamAsync(id, caller, ct);
    if (!result.IsSuccess) return MapError(result.Error!);
    return Results.Stream(result.Value!, "application/octet-stream");
}).RequireAuthorization();

// ─── GET /v1/categories ───────────────────────────────────────────────────────
app.MapGet("/v1/categories", async (IUnitOfWork uow, CancellationToken ct) =>
{
    var categories = await uow.Categories.ListAllAsync(ct);
    return Results.Ok(categories);
}).RequireAuthorization();

app.Run();

public partial class Program { }
