// Wave 2 stub — services not yet implemented. All tests skip until Wave 2.
// When Wave 2 implements FileManagementService, remove [Fact(Skip=...)] attributes.
namespace Storage.Application.Tests;

public class FileManagementServiceTests
{
    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task SoftDelete_ReadyFile_SetsDeletedStatus() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task HardDelete_WithoutAdminScope_ReturnsAccessDenied() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task ListFiles_WithCursor_ReturnsPaginatedResults() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task PatchFile_UpdatesMetadataAndInvalidatesCache() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task CreateVersion_AddsFileVersionEntry() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task GenerateShareLink_StoresTokenInCache() => Task.CompletedTask;
}
