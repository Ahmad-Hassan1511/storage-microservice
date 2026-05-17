using System.Security.Cryptography;
using System.Text;
using NSubstitute;
using FluentAssertions;
using Storage.Application.Abstractions;
using Storage.Application.Common;
using Storage.Application.DTOs;
using Storage.Application.Events;
using Storage.Application.Services;
using Storage.Domain.Entities;
using Storage.Domain.Enums;
using Storage.Domain.ValueObjects;
using DomainFile = Storage.Domain.Entities.File;

namespace Storage.Application.Tests;

public class UploadServiceTests
{
    private readonly IFileStorageProvider _storage = Substitute.For<IFileStorageProvider>();
    private readonly ICacheProvider _cache = Substitute.For<ICacheProvider>();
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IFileRepository _fileRepo = Substitute.For<IFileRepository>();
    private readonly IFileCategoryRepository _categoryRepo = Substitute.For<IFileCategoryRepository>();
    private readonly TimeProvider _time = Substitute.For<TimeProvider>();
    private readonly UploadService _sut;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly CallerContext _caller;
    private readonly DateTimeOffset _now = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);

    public UploadServiceTests()
    {
        _uow.Files.Returns(_fileRepo);
        _uow.Categories.Returns(_categoryRepo);
        _time.GetUtcNow().Returns(_now);
        _sut = new UploadService(_storage, _cache, _eventBus, _uow, _time);
        _caller = new CallerContext(_tenantId, "service", "billing-svc", []);
    }

    private FileCategory MakeCategory(
        long maxSizeBytes = 10_000,
        string[]? allowedMimeTypes = null,
        string[]? allowedExtensions = null,
        string[]? allowedOwnerServices = null,
        bool isLargeFile = false,
        long? multipartThreshold = null) => new FileCategory
        {
            Id = "docs",
            MaxSizeBytes = maxSizeBytes,
            AllowedMimeTypes = allowedMimeTypes ?? [],
            AllowedExtensions = allowedExtensions ?? [],
            AllowedOwnerServices = allowedOwnerServices ?? ["billing-svc"],
            IsLargeFile = isLargeFile,
            MultipartThresholdBytes = multipartThreshold
        };

    private void SetupNoCachedHash() =>
        _cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
              .Returns((string?)null);

    private void SetupStorageForUpload(string? url = "https://upload.example.com/key", bool proxyRequired = false) =>
        _storage.GetUploadTargetAsync(Arg.Any<StoragePutRequest>(), Arg.Any<CancellationToken>())
                .Returns(new StoragePutResult(url, new Dictionary<string, string>(), proxyRequired));

    private static string ComputePayloadHash(string categoryId, string fileName, long sizeBytes, string ownerService)
    {
        var raw = $"{categoryId}|{fileName}|{sizeBytes}|{ownerService}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }

    [Fact]
    public async Task InitiateUpload_ValidRequest_ReturnsStructuredResponse()
    {
        // Arrange
        var req = new InitiateUploadRequest("docs", "report.pdf", "application/pdf", 1024, "key-1", "billing-svc");
        _categoryRepo.GetByIdAsync("docs", Arg.Any<CancellationToken>()).Returns(MakeCategory());
        SetupNoCachedHash();
        SetupStorageForUpload();

        // Act
        var result = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.FileId.Should().NotBe(Guid.Empty);
        result.Value.ExpiresAt.Should().BeAfter(_now);
        result.Value.ProxyRequired.Should().BeFalse();
        await _fileRepo.Received(1).AddAsync(Arg.Any<DomainFile>(), Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Any<FileCreatedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateUpload_UnknownCategory_ReturnsNotFoundError()
    {
        // Arrange
        var req = new InitiateUploadRequest("unknown", "file.pdf", "application/pdf", 100, "key-2", "billing-svc");
        _categoryRepo.GetByIdAsync("unknown", Arg.Any<CancellationToken>()).Returns((FileCategory?)null);
        SetupNoCachedHash();

        // Act
        var result = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<NotFoundError>();
        await _fileRepo.DidNotReceive().AddAsync(Arg.Any<DomainFile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateUpload_OversizeFile_ReturnsPolicyViolationError413()
    {
        // Arrange
        var req = new InitiateUploadRequest("docs", "huge.pdf", "application/pdf", 50_000, "key-3", "billing-svc");
        _categoryRepo.GetByIdAsync("docs", Arg.Any<CancellationToken>()).Returns(MakeCategory(maxSizeBytes: 10_000));
        SetupNoCachedHash();

        // Act
        var result = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<PolicyViolationError>()
              .Which.HttpStatusHint.Should().Be(413);
    }

    [Fact]
    public async Task InitiateUpload_DisallowedMimeType_ReturnsPolicyViolationError415()
    {
        // Arrange
        var req = new InitiateUploadRequest("docs", "photo.jpg", "image/jpeg", 1024, "key-4", "billing-svc");
        _categoryRepo.GetByIdAsync("docs", Arg.Any<CancellationToken>())
            .Returns(MakeCategory(allowedMimeTypes: ["application/pdf"]));
        SetupNoCachedHash();

        // Act
        var result = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<PolicyViolationError>()
              .Which.HttpStatusHint.Should().Be(415);
    }

    [Fact]
    public async Task InitiateUpload_DisallowedExtension_ReturnsPolicyViolationError415()
    {
        // Arrange
        var req = new InitiateUploadRequest("docs", "malware.exe", "application/pdf", 1024, "key-5", "billing-svc");
        _categoryRepo.GetByIdAsync("docs", Arg.Any<CancellationToken>())
            .Returns(MakeCategory(allowedExtensions: [".pdf"]));
        SetupNoCachedHash();

        // Act
        var result = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<PolicyViolationError>()
              .Which.HttpStatusHint.Should().Be(415);
    }

    [Fact]
    public async Task InitiateUpload_ForbiddenOwnerService_ReturnsAccessDeniedError()
    {
        // Arrange
        var caller = new CallerContext(_tenantId, "service", "unknown-svc", []);
        var req = new InitiateUploadRequest("docs", "file.pdf", "application/pdf", 1024, "key-6", "unknown-svc");
        _categoryRepo.GetByIdAsync("docs", Arg.Any<CancellationToken>())
            .Returns(MakeCategory(allowedOwnerServices: ["billing-svc"]));
        SetupNoCachedHash();

        // Act
        var result = await _sut.InitiateUploadAsync(req, caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<AccessDeniedError>();
        await _fileRepo.DidNotReceive().AddAsync(Arg.Any<DomainFile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateUpload_LargeFileCategory_MultipartRequired()
    {
        // Arrange — small SizeBytes, but IsLargeFile=true forces multipart
        var req = new InitiateUploadRequest("videos", "tiny.mp4", "video/mp4", 100, "key-7", "billing-svc");
        _categoryRepo.GetByIdAsync("videos", Arg.Any<CancellationToken>())
            .Returns(new FileCategory
            {
                Id = "videos",
                MaxSizeBytes = 1_000_000,
                AllowedOwnerServices = ["billing-svc"],
                IsLargeFile = true
            });
        SetupNoCachedHash();
        SetupStorageForUpload();

        // Act
        var result = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MultipartRequired.Should().BeTrue();
    }

    [Fact]
    public async Task InitiateUpload_SameKeyAndPayload_ReturnsExistingFileId()
    {
        // Arrange
        var req = new InitiateUploadRequest("docs", "report.pdf", "application/pdf", 1024, "idem-key", "billing-svc");
        var payloadHash = ComputePayloadHash("docs", "report.pdf", 1024, "billing-svc");
        var hashCacheKey = "idempotency:idem-key:hash";
        var responseCacheKey = "idempotency:idem-key:response";

        _categoryRepo.GetByIdAsync("docs", Arg.Any<CancellationToken>()).Returns(MakeCategory());
        SetupStorageForUpload();

        // First call: cache miss
        _cache.GetAsync<string>(hashCacheKey, Arg.Any<CancellationToken>()).Returns((string?)null);

        // Act - first call
        var result1 = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        // Setup cache to reflect what was stored during first call
        _cache.GetAsync<string>(hashCacheKey, Arg.Any<CancellationToken>()).Returns(payloadHash);
        _cache.GetAsync<InitiateUploadResponse>(responseCacheKey, Arg.Any<CancellationToken>())
              .Returns(result1.Value!);

        // Act - second call (idempotency hit)
        var result2 = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);

        // Assert
        result2.IsSuccess.Should().BeTrue();
        result2.Value!.FileId.Should().Be(result1.Value!.FileId);
        await _fileRepo.Received(1).AddAsync(Arg.Any<DomainFile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateUpload_SameKeyDifferentPayload_ReturnsIdempotencyConflictError()
    {
        // Arrange — cache has hash from a previous call with SizeBytes=1024
        var storedHash = ComputePayloadHash("docs", "report.pdf", 1024, "billing-svc");
        var hashCacheKey = "idempotency:idem-key:hash";
        _cache.GetAsync<string>(hashCacheKey, Arg.Any<CancellationToken>()).Returns(storedHash);

        // New request has different SizeBytes → different payload hash
        var req = new InitiateUploadRequest("docs", "report.pdf", "application/pdf", 99_999, "idem-key", "billing-svc");

        // Act
        var result = await _sut.InitiateUploadAsync(req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<IdempotencyConflictError>();
    }

    [Fact]
    public async Task InitiateUpload_SameKeyDifferentOwnerService_ReturnsIdempotencyConflictError()
    {
        // Arrange — cache has hash from billing-svc; now shipping-svc uses the same key
        var storedHash = ComputePayloadHash("docs", "report.pdf", 1024, "billing-svc");
        var hashCacheKey = "idempotency:idem-key:hash";
        _cache.GetAsync<string>(hashCacheKey, Arg.Any<CancellationToken>()).Returns(storedHash);

        var req = new InitiateUploadRequest("docs", "report.pdf", "application/pdf", 1024, "idem-key", "shipping-svc");
        var caller = new CallerContext(_tenantId, "service", "shipping-svc", []);

        // Act
        var result = await _sut.InitiateUploadAsync(req, caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<IdempotencyConflictError>("OwnerService is part of the payload hash");
    }

    [Fact]
    public async Task CompleteUpload_ValidChecksum_TransitionsToScanningAndPublishesEvent()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var storageKey = StorageKey.Create(_tenantId, new DateOnly(2026, 5, 16), fileId);
        var checksumHex = new string('a', 64); // valid 64-char lowercase hex

        var file = DomainFile.Rehydrate(
            fileId, _tenantId, "billing-svc", "docs", "report.pdf", "application/pdf",
            1024, FileStatus.Pending, storageKey, Visibility.Private, DateTime.UtcNow);
        file.SetStorageDetails(storageKey, new Checksum(checksumHex));

        _fileRepo.GetByIdAsync(fileId, _tenantId, Arg.Any<CancellationToken>()).Returns(file);

        var req = new CompleteUploadRequest(checksumHex, 1024);

        // Act
        var result = await _sut.CompleteUploadAsync(fileId, req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("scanning");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _eventBus.Received(1).PublishAsync(Arg.Any<FileUploadedIntegrationEvent>(), Arg.Any<CancellationToken>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FileCreatedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteUpload_ChecksumMismatch_ReturnsChecksumMismatchError()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var storageKey = StorageKey.Create(_tenantId, new DateOnly(2026, 5, 16), fileId);
        var storedHex = new string('a', 64);
        var suppliedHex = new string('b', 64); // different checksum

        var file = DomainFile.Rehydrate(
            fileId, _tenantId, "billing-svc", "docs", "report.pdf", "application/pdf",
            1024, FileStatus.Pending, storageKey, Visibility.Private, DateTime.UtcNow);
        file.SetStorageDetails(storageKey, new Checksum(storedHex));

        _fileRepo.GetByIdAsync(fileId, _tenantId, Arg.Any<CancellationToken>()).Returns(file);

        var req = new CompleteUploadRequest(suppliedHex, 1024);

        // Act
        var result = await _sut.CompleteUploadAsync(fileId, req, _caller, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<ChecksumMismatchError>();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FileUploadedIntegrationEvent>(), Arg.Any<CancellationToken>());
    }
}
