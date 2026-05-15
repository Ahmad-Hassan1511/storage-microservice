// Wave 2 stub — services not yet implemented. All tests skip until Wave 2.
// When Wave 2 implements UploadService, remove [Fact(Skip=...)] attributes.
namespace Storage.Application.Tests;

public class UploadServiceTests
{
    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_ValidRequest_ReturnsStructuredResponse() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_UnknownCategory_ReturnsNotFoundError() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_OversizeFile_ReturnsPolicyViolationError413() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_DisallowedMimeType_ReturnsPolicyViolationError415() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_DisallowedExtension_ReturnsPolicyViolationError415() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_ForbiddenOwnerService_ReturnsAccessDeniedError() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_LargeFileCategory_MultipartRequired() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_SameKeyAndPayload_ReturnsExistingFileId() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_SameKeyDifferentPayload_ReturnsIdempotencyConflictError() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task InitiateUpload_SameKeyDifferentOwnerService_ReturnsIdempotencyConflictError() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task CompleteUpload_ValidChecksum_TransitionsToScanningAndPublishesEvent() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task CompleteUpload_ChecksumMismatch_ReturnsChecksumMismatchError() => Task.CompletedTask;
}
