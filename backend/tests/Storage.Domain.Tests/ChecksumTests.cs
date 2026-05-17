using FluentAssertions;
using Storage.Domain.Common;
using Storage.Domain.ValueObjects;

namespace Storage.Domain.Tests;

public class ChecksumTests
{
    [Fact]
    public void Constructor_WithUppercaseHex64_NormalizesToLowercase()
    {
        var upper = new string('A', 64);
        var expected = new string('a', 64);

        var checksum = new Checksum(upper);

        checksum.Value.Should().Be(expected);
    }

    [Fact]
    public void Constructor_WithValidLowercaseHex64_AcceptsUnchanged()
    {
        var valid = new string('a', 62) + "1f";

        var checksum = new Checksum(valid);

        checksum.Value.Should().Be(valid);
    }

    [Theory]
    [InlineData("not-hex")]
    [InlineData("abc123")]
    public void Constructor_WithInvalidHex_ThrowsInvalidChecksumException(string invalid)
    {
        var act = () => new Checksum(invalid);
        act.Should().Throw<InvalidChecksumException>();
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException()
    {
        var act = () => new Checksum(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
