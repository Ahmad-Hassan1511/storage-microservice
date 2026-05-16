using Microsoft.EntityFrameworkCore;

namespace Profile;

public sealed class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
}

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public string? AvatarStatus { get; set; }
    public Guid? AvatarFileId { get; set; }
    public string? AvatarUrl256 { get; set; }
    public string? AvatarUrl64 { get; set; }
}

public sealed record AvatarUploadRequest(string MimeType, long SizeBytes);
