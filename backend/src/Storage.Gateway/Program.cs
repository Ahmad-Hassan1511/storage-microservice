using Microsoft.AspNetCore.Authentication.JwtBearer;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// SETUP-04: JWT validation against Keycloak storage-service realm.
// The authority URL uses the Docker service name "keycloak" (not localhost) so that
// the Gateway container can reach Keycloak on the internal Docker network.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("keycloak", options =>
    {
        options.Authority = builder.Configuration["Keycloak:Authority"];
        options.RequireHttpsMetadata = false;  // Demo/local only — HTTPS required in production
        options.Audience = "storage-api";
        options.TokenValidationParameters.ValidateAudience = true;
    });

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();

// Gateway health probe (does not go through Ocelot routing, does not require auth)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ocelot-gateway" }))
   .AllowAnonymous();

await app.UseOcelot();

app.Run();
