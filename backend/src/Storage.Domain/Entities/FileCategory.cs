namespace Storage.Domain.Entities;

public class FileCategory
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public long MaxSizeBytes { get; set; }
    public string[] AllowedMimeTypes { get; set; } = [];
    public string[] AllowedExtensions { get; set; } = [];
    public bool IsLargeFile { get; set; }
    public bool SupportsPreview { get; set; }
    public bool AntivirusRequired { get; set; }
    public bool RequiresAiValidation { get; set; }
    public string? AiValidationStrategy { get; set; }

    /// <summary>
    /// Validates a file against this category's policy.
    /// Returns (IsValid, Error) — does NOT throw; use this to get a result.
    /// </summary>
    public (bool IsValid, string? Error) Validate(File file)
    {
        if (file.SizeBytes > MaxSizeBytes)
            return (false, $"File size {file.SizeBytes} exceeds the maximum allowed {MaxSizeBytes} bytes for category '{Id}'.");

        if (AllowedMimeTypes.Length > 0 && !AllowedMimeTypes.Contains(file.MimeType, StringComparer.OrdinalIgnoreCase))
            return (false, $"MIME type '{file.MimeType}' is not allowed for category '{Id}'. Allowed: {string.Join(", ", AllowedMimeTypes)}.");

        if (AllowedExtensions.Length > 0)
        {
            var extension = System.IO.Path.GetExtension(file.OriginalFileName);
            if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                return (false, $"Extension '{extension}' is not allowed for category '{Id}'. Allowed: {string.Join(", ", AllowedExtensions)}.");
        }

        return (true, null);
    }
}
