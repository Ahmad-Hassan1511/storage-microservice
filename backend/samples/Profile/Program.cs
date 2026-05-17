using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Profile;
using Profile.Controllers;
using Scalar.AspNetCore;
using Storage.Sdk;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddDbContext<ProfileDbContext>(o =>
    o.UseSqlite("Data Source=profiles.db"));

// JWT auth — dev mode uses same HS256 key as Storage.Api
var authAuthority = builder.Configuration["Auth:Authority"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authAuthority;
        options.Audience = builder.Configuration["Auth:Audience"] ?? "profile-service";
        options.RequireHttpsMetadata = false;
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

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

builder.Services.AddStorageClient(builder.Configuration);

// MassTransit — InMemory in Development (no RabbitMQ needed), RabbitMQ otherwise
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AvatarFileReadyHandler>();
    if (builder.Environment.IsDevelopment())
    {
        x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
    }
    else
    {
        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Uri"] ?? "amqp://guest:guest@localhost:5672");
            cfg.ConfigureEndpoints(ctx);
        });
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ProfileDbContext>().Database.EnsureCreated();

app.MapOpenApi();
app.MapScalarApiReference("/swagger");
app.MapGet("/swagger", () => Results.Redirect("/swagger/v1")).ExcludeFromDescription();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
