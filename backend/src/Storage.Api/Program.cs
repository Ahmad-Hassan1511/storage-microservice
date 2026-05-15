var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Phase 1: stub health endpoint only.
// Full API implementation is Phase 6.
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "storage-api" }))
   .AllowAnonymous();

app.Run();
