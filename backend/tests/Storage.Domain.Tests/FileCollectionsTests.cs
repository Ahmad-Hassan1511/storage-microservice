using FluentAssertions;
using Storage.Domain.Entities;
using DomainFile = Storage.Domain.Entities.File;

namespace Storage.Domain.Tests;

public class FileCollectionsTests
{
    private static DomainFile CreateNewFile() =>
        DomainFile.Create(Guid.NewGuid(), "test-service", "documents", "test.pdf", "application/pdf", 1024);

    [Fact]
    public void File_Versions_IsNonNull_IReadOnlyList()
    {
        var file = CreateNewFile();
        Assert.IsAssignableFrom<IReadOnlyList<FileVersion>>(file.Versions);
    }

    [Fact]
    public void File_Permissions_IsNonNull_IReadOnlyList()
    {
        var file = CreateNewFile();
        Assert.IsAssignableFrom<IReadOnlyList<FilePermission>>(file.Permissions);
    }

    [Fact]
    public void File_Tags_IsNonNull_IReadOnlyList()
    {
        var file = CreateNewFile();
        Assert.IsAssignableFrom<IReadOnlyList<FileTag>>(file.Tags);
    }

    [Fact]
    public void File_AuditEntries_IsNonNull_IReadOnlyList()
    {
        var file = CreateNewFile();
        Assert.IsAssignableFrom<IReadOnlyList<AuditEntry>>(file.AuditEntries);
    }

    [Fact]
    public void File_Versions_IsEmptyOnCreation()
    {
        var file = CreateNewFile();
        file.Versions.Should().NotBeNull();
        file.Versions.Count.Should().Be(0);
    }

    [Fact]
    public void File_Permissions_IsEmptyOnCreation()
    {
        var file = CreateNewFile();
        file.Permissions.Should().NotBeNull();
        file.Permissions.Count.Should().Be(0);
    }

    [Fact]
    public void File_Tags_IsEmptyOnCreation()
    {
        var file = CreateNewFile();
        file.Tags.Should().NotBeNull();
        file.Tags.Count.Should().Be(0);
    }

    [Fact]
    public void File_AuditEntries_IsEmptyOnCreation()
    {
        var file = CreateNewFile();
        file.AuditEntries.Should().NotBeNull();
        file.AuditEntries.Count.Should().Be(0);
    }
}
