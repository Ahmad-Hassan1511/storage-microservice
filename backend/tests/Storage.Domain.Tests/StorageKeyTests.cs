using FluentAssertions;
using Storage.Domain.Common;
using Storage.Domain.ValueObjects;

namespace Storage.Domain.Tests;

public class StorageKeyTests
{
    [Fact]
    public void Create_ProducesCorrectFormat()
    {
        var tenantId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var date = new DateOnly(2024, 6, 15);

        var key = StorageKey.Create(tenantId, date, fileId);

        key.Value.Should().Be($"{tenantId:D}/2024/06/15/{fileId:D}");
    }

    [Fact]
    public void Constructor_WithValidGuidDateGuidString_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var validKey = $"{tenantId:D}/2024/01/01/{fileId:D}";

        var act = () => new StorageKey(validKey);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("not-a-valid-key")]
    [InlineData("tenantid/2024/01/01/fileid")]
    [InlineData("")]
    public void Constructor_WithInvalidString_ThrowsInvalidStorageKeyException(string invalidKey)
    {
        var act = () => new StorageKey(invalidKey);
        act.Should().Throw<InvalidStorageKeyException>();
    }

    [Fact]
    public void Create_ProducedKey_IsAcceptedByConstructor()
    {
        var tenantId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var date = new DateOnly(2025, 3, 7);

        var created = StorageKey.Create(tenantId, date, fileId);
        var roundTripped = new StorageKey(created.Value);

        roundTripped.Value.Should().Be(created.Value);
    }
}
