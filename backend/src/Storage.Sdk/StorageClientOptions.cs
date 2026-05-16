namespace Storage.Sdk;

public sealed class StorageClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5100";
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public long MultipartThresholdBytes { get; set; } = 5 * 1024 * 1024; // 5 MB
    public int MultipartPartSizeBytes { get; set; } = 5 * 1024 * 1024;   // 5 MB per part
    public int MultipartConcurrency { get; set; } = 4;
}
