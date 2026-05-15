using System.Text.RegularExpressions;
using Storage.Domain.Common;

namespace Storage.Domain.ValueObjects;

public sealed class Checksum
{
    private static readonly Regex _sha256Hex = new(@"^[0-9a-f]{64}$", RegexOptions.Compiled);

    public string Value { get; }

    public Checksum(string value)
    {
        var normalized = value?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(value));
        if (!_sha256Hex.IsMatch(normalized))
            throw new InvalidChecksumException(value);
        Value = normalized;
    }
}
