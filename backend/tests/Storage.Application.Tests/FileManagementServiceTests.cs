using NSubstitute;
using FluentAssertions;
using Storage.Application.Abstractions;
using Storage.Application.Common;
using Storage.Application.DTOs;
using Storage.Application.Services;
using Storage.Domain.Enums;
using DomainFile = Storage.Domain.Entities.File;

namespace Storage.Application.Tests;

public class FileManagementServiceTests
{
    private readonly IFileStorageProvider _storage = Substitute.For<IFileStorageProvider>();
    private readonly ICacheProvider _cache = Substitute.For<ICacheProvider>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _fileRepo = Substitute.For<IFileRepository>();
    private readonly IFileVersionRepository _versionRepo = Substitute.For<IFileVersionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly FileManagementService _sut;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly CallerContext _adminCaller;
    private readonly CallerContext _regularCaller;

    public FileManagementServiceTests()
    {
        _uow.Files.Returns(_fileRepo);
        _uow.FileVersions.Returns(_versionRepo);
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _sut = new FileManagementService(_storage, _cache, _uow, _timeProvider);

        _adminCaller = new CallerContext(_tenantId, "service", "svc-admin", new[] { "storage.admin" });
        _regularCaller = new CallerContext(_tenantId, "service", "svc-regular", Array.Empty<string>());
    }

    private DomainFile CreateReadyFile()
    {
        var file = DomainFile.Create(_tenantId, "svc", "docs", "report.pdf", "application/pdf", 1024);
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        return file;
    }

    [Fact]
    public async Task SoftDelete_ReadyFile_SetsDeletedStatus()
    {
        // Arrange
        var file = CreateReadyFile();
        _fileRepo.GetByIdAsync(file.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(file);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        // Act
        var result = await _sut.SoftDeleteAsync(file.Id, _regularCaller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        file.Status.Should().Be(FileStatus.Deleted);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync($"file:{file.Id}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HardDelete_WithoutAdminScope_ReturnsAccessDenied()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        // Act
        var result = await _sut.HardDeleteAsync(fileId, _regularCaller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<AccessDeniedError>();
        await _storage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileRepo.DidNotReceive().HardDeleteAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListFiles_WithCursor_ReturnsPaginatedResults()
    {
        // Arrange
        var pageSize = 2;
        // Return pageSize + 1 items to signal there's a next page
        var files = Enumerable.Range(0, pageSize + 1)
            .Select(_ => DomainFile.Create(_tenantId, "svc", "docs", "file.pdf", "application/pdf", 512))
            .ToList();
        var query = new FileListQuery(_tenantId, null, null, null, pageSize, null);
        _fileRepo.ListAsync(Arg.Any<FileListQuery>(), Arg.Any<CancellationToken>())
            .Returns(files.AsReadOnly());

        // Act
        var result = await _sut.ListFilesAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(pageSize);
        result.Value.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PatchFile_UpdatesMetadataAndInvalidatesCache()
    {
        // Arrange
        var file = CreateReadyFile();
        _fileRepo.GetByIdAsync(file.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(file);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var req = new PatchFileRequest("updated-name.pdf", new Dictionary<string, string> { { "env", "prod" } }, null);

        // Act
        var result = await _sut.PatchFileAsync(file.Id, req, _regularCaller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        file.OriginalFileName.Should().Be("updated-name.pdf");
        file.Tags.Should().ContainSingle(t => t.Key == "env" && t.Value == "prod");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync($"file:{file.Id}", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateVersion_AddsFileVersionEntry()
    {
        // Arrange
        var file = CreateReadyFile();
        _fileRepo.GetByIdAsync(file.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(file);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var storageKey = $"{_tenantId:D}/2026/05/16/{Guid.NewGuid():D}";
        var checksum = new string('a', 64); // 64 hex chars for sha256

        // Act
        var result = await _sut.CreateVersionAsync(file.Id, storageKey, checksum, 2048L, _adminCaller, CancellationToken.None);

        // Assert — file.Versions.Count is 0 in-memory (AddAsync is mocked), so versionNumber = 0+1 = 1
        result.IsSuccess.Should().BeTrue();
        result.Value!.VersionNumber.Should().Be(1);
        await _versionRepo.Received(1).AddAsync(
            Arg.Is<Storage.Domain.Entities.FileVersion>(v => v.VersionNumber == 1),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateShareLink_StoresTokenInCache()
    {
        // Arrange
        var file = CreateReadyFile();
        _fileRepo.GetByIdAsync(file.Id, _tenantId, Arg.Any<CancellationToken>()).Returns(file);
        var ttl = TimeSpan.FromHours(1);

        // Act
        var result = await _sut.GenerateShareLinkAsync(file.Id, ttl, _regularCaller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        var token = result.Value!;
        await _cache.Received(1).SetAsync(
            Arg.Is<string>(k => k.StartsWith($"share:{file.Id}:")),
            Arg.Any<object>(),
            Arg.Is<TimeSpan?>(t => t == ttl),
            Arg.Any<CancellationToken>());
    }
}
