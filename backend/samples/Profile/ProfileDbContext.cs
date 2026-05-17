using Microsoft.EntityFrameworkCore;

namespace Profile;

public sealed class ProfileDbContext(DbContextOptions<ProfileDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProfile>().HasKey(p => p.UserId);
    }
}

public sealed class UserProfile
{
    public string UserId { get; set; } = string.Empty;
    public string? AvatarStatus { get; set; }
    public Guid? AvatarFileId { get; set; }
    public string? AvatarUrl256 { get; set; }
    public string? AvatarUrl64 { get; set; }
}

public sealed record AvatarUploadRequest(string MimeType, long SizeBytes);
public sealed record UpdateAvatarRequest(Guid FileId);
public sealed record InitiateAvatarUploadRequest(string FileName, string MimeType, long SizeBytes);
public sealed record CompleteAvatarUploadRequest(string ChecksumSha256, long SizeBytes);
