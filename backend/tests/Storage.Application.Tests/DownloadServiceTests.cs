// Wave 2 stub — services not yet implemented. All tests skip until Wave 2.
// When Wave 2 implements DownloadService, remove [Fact(Skip=...)] attributes.
namespace Storage.Application.Tests;

public class DownloadServiceTests
{
    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task GetFile_ReadyFile_ReturnsPresignedUrl() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task GetFile_PendingFile_ReturnsNotFound() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task GetFile_CrossTenant_ReturnsNotFound() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task GetFile_InsufficientPermission_ReturnsAccessDenied() => Task.CompletedTask;

    [Fact(Skip = "Wave 0 stub — implementation pending")]
    public Task GetFileStream_ProxyPath_CallsOpenReadStream() => Task.CompletedTask;
}
