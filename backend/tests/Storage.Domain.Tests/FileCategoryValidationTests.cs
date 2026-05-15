using FluentAssertions;
using Storage.Domain.Entities;
using Storage.Domain.Enums;
using Storage.Domain.ValueObjects;
using DomainFile = Storage.Domain.Entities.File;

namespace Storage.Domain.Tests;

public class FileCategoryValidationTests
{
    private static FileCategory CreateCategory(
        long maxSizeBytes = 10_000_000,
        string[]? allowedMimeTypes = null,
        string[]? allowedExtensions = null) => new FileCategory
    {
        Id = "documents",
        DisplayName = "Documents",
        MaxSizeBytes = maxSizeBytes,
        AllowedMimeTypes = allowedMimeTypes ?? ["application/pdf", "image/jpeg"],
        AllowedExtensions = allowedExtensions ?? [".pdf", ".jpg"],
        IsLargeFile = false,
        SupportsPreview = true,
        AntivirusRequired = true,
        RequiresAiValidation = false,
        AiValidationStrategy = null
    };

    private static DomainFile CreateFile(string mimeType = "application/pdf", long sizeBytes = 1024, string fileName = "test.pdf") =>
        DomainFile.Create(Guid.NewGuid(), "test-service", "documents", fileName, mimeType, sizeBytes);

    [Fact]
    public void Validate_FileSizeExceedsMax_ReturnsFailure()
    {
        var category = CreateCategory(maxSizeBytes: 1_000_000);
        var file = CreateFile(sizeBytes: 2_000_000);

        var (isValid, error) = category.Validate(file);

        isValid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_MimeTypeNotAllowed_ReturnsFailure()
    {
        var category = CreateCategory(allowedMimeTypes: ["image/jpeg"]);
        var file = CreateFile(mimeType: "application/pdf");

        var (isValid, error) = category.Validate(file);

        isValid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_ExtensionNotAllowed_ReturnsFailure()
    {
        var category = CreateCategory(allowedExtensions: [".jpg"]);
        var file = CreateFile(fileName: "document.pdf", mimeType: "image/jpeg");

        var (isValid, error) = category.Validate(file);

        isValid.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_ValidFile_ReturnsSuccess()
    {
        var category = CreateCategory(
            maxSizeBytes: 10_000_000,
            allowedMimeTypes: ["application/pdf"],
            allowedExtensions: [".pdf"]);
        var file = CreateFile(mimeType: "application/pdf", sizeBytes: 500_000, fileName: "valid.pdf");

        var (isValid, error) = category.Validate(file);

        isValid.Should().BeTrue();
        error.Should().BeNull();
    }
}
