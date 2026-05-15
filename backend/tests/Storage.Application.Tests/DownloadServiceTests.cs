using NSubstitute;
using FluentAssertions;
using Storage.Application.Abstractions;
using Storage.Application.Common;
using Storage.Application.DTOs;
using Storage.Application.Services;
using Storage.Domain.Entities;
using Storage.Domain.Enums;
using Storage.Domain.ValueObjects;
using DomainFile = Storage.Domain.Entities.File;
using RangeHeader = System.Net.Http.Headers.RangeHeaderValue;

namespace Storage.Application.Tests;

public class DownloadServiceTests
{
    private readonly IFileStorageProvider _storage = Substitute.For<IFileStorageProvider>();
    private readonly ICacheProvider _cache = Substitute.For<ICacheProvider>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _fileRepo = Substitute.For<IFileRepository>();
    private readonly IAuditRepository _auditRepo = Substitute.For<IAuditRepository>();
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();
    private readonly DownloadService _sut;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _fileId = Guid.NewGuid();
    private readonly CallerContext _caller;
    private readonly StorageKey _storageKey;

    public DownloadServiceTests()
    {
        _uow.Files.Returns(_fileRepo);
        _uow.Audit.Returns(_auditRepo);
        _time.GetUtcNow().Returns(new DateTimeOffset(2026, 5, 16, 0, 0, 0, TimeSpan.Zero));
        _sut = new DownloadService(_storage, _cache, _uow, _time);

        _caller = new CallerContext(_tenantId, "service", "svc-consumer", []);
        _storageKey = StorageKey.Create(_tenantId, new DateOnly(2026, 5, 16), _fileId);
    }

    private DomainFile MakeReadyFile(IEnumerable<FilePermission>? perms = null)
    {
        var permission = perms ?? [FilePermission.Create(_fileId, "service", "svc-consumer", Permission.Read)];
        return DomainFile.Rehydrate(
            _fileId, _tenantId, "owner-svc", "docs", "report.pdf", "application/pdf",
            1024, FileStatus.Ready, _storageKey, Visibility.Private,
            DateTime.UtcNow, permission);
    }

    [Fact]
    public async Task GetFile_ReadyFile_ReturnsPresignedUrl()
    {
        // Arrange
        var file = MakeReadyFile();
        _cache.GetAsync<GetFileResponse>($"file:{_fileId}", Arg.Any<CancellationToken>())
            .Returns((GetFileResponse?)null);
        _fileRepo.GetByIdAsync(_fileId, _tenantId, Arg.Any<CancellationToken>())
            .Returns(file);
        _storage.GetDownloadTargetAsync("files", _storageKey.Value, TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>())
            .Returns(new StorageGetResult("https://presigned.example.com/file", false));

        // Act
        var result = await _sut.GetFileAsync(_fileId, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.DownloadUrl.Should().Be("https://presigned.example.com/file");
        await _storage.Received(1).GetDownloadTargetAsync("files", _storageKey.Value, TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFile_PendingFile_ReturnsNotFound()
    {
        // Arrange
        var file = DomainFile.Rehydrate(
            _fileId, _tenantId, "owner-svc", "docs", "report.pdf", "application/pdf",
            1024, FileStatus.Pending, _storageKey, Visibility.Private, DateTime.UtcNow);
        _cache.GetAsync<GetFileResponse>($"file:{_fileId}", Arg.Any<CancellationToken>())
            .Returns((GetFileResponse?)null);
        _fileRepo.GetByIdAsync(_fileId, _tenantId, Arg.Any<CancellationToken>())
            .Returns(file);

        // Act
        var result = await _sut.GetFileAsync(_fileId, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<NotFoundError>();
        await _storage.DidNotReceive().GetDownloadTargetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetFile_CrossTenant_ReturnsNotFound()
    {
        // Arrange — uow returns null because TenantId doesn't match
        _cache.GetAsync<GetFileResponse>($"file:{_fileId}", Arg.Any<CancellationToken>())
            .Returns((GetFileResponse?)null);
        _fileRepo.GetByIdAsync(_fileId, _tenantId, Arg.Any<CancellationToken>())
            .Returns((DomainFile?)null);

        // Act
        var result = await _sut.GetFileAsync(_fileId, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<NotFoundError>("anti-enumeration: cross-tenant is 404, not 403");
    }

    [Fact]
    public async Task GetFile_InsufficientPermission_ReturnsAccessDenied()
    {
        // Arrange — file exists but no CanRead permission for caller
        var file = MakeReadyFile(perms: []); // empty permissions list
        _cache.GetAsync<GetFileResponse>($"file:{_fileId}", Arg.Any<CancellationToken>())
            .Returns((GetFileResponse?)null);
        _fileRepo.GetByIdAsync(_fileId, _tenantId, Arg.Any<CancellationToken>())
            .Returns(file);

        // Act
        var result = await _sut.GetFileAsync(_fileId, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<AccessDeniedError>();
    }

    [Fact]
    public async Task GetFileStream_ProxyPath_CallsOpenReadStream()
    {
        // Arrange
        var file = MakeReadyFile();
        var fakeStream = new MemoryStream([1, 2, 3]);
        _cache.GetAsync<GetFileResponse>($"file:{_fileId}", Arg.Any<CancellationToken>())
            .Returns((GetFileResponse?)null);
        _fileRepo.GetByIdAsync(_fileId, _tenantId, Arg.Any<CancellationToken>())
            .Returns(file);
        _storage.OpenReadStreamAsync("files", _storageKey.Value, null, Arg.Any<CancellationToken>())
            .Returns(fakeStream);

        // Act
        var result = await _sut.GetFileStreamAsync(_fileId, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(fakeStream);
        await _storage.Received(1).OpenReadStreamAsync("files", _storageKey.Value, null, Arg.Any<CancellationToken>());
        await _auditRepo.Received(1).AddAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
