using MassTransit;
using Microsoft.EntityFrameworkCore;
using Profile;
using Storage.Sdk;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ProfileDbContext>(o => o.UseInMemoryDatabase("profiles"));
builder.Services.AddStorageClient(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AvatarFileReadyHandler>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Uri"] ?? "amqp://guest:guest@localhost:5672");
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();

// POST /api/profiles/{userId}/avatar — initiate avatar upload
app.MapPost("/api/profiles/{userId:guid}/avatar", async (
    Guid userId,
    AvatarUploadRequest req,
    ProfileDbContext db,
    IStorageClient storage,
    CancellationToken ct) =>
{
    var profile = await db.UserProfiles.FindAsync([userId], ct)
        ?? new UserProfile { UserId = userId };

    profile.AvatarStatus = "uploading";
    if (db.Entry(profile).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
        db.UserProfiles.Add(profile);

    await db.SaveChangesAsync(ct);

    return Results.Accepted($"/api/profiles/{userId}", new
    {
        userId,
        status = "uploading",
        message = "Upload the avatar bytes to the presigned URL then call /complete",
    });
});

// GET /api/profiles/{userId}
app.MapGet("/api/profiles/{userId:guid}", async (
    Guid userId,
    ProfileDbContext db,
    CancellationToken ct) =>
{
    var profile = await db.UserProfiles.FindAsync([userId], ct);
    return profile is null ? Results.NotFound() : Results.Ok(profile);
});

app.Run();
