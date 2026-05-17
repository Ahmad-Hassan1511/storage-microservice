using System.Text.RegularExpressions;
using Storage.Domain.Common;

namespace Storage.Domain.ValueObjects;

public sealed class StorageKey
{
    // Pattern: <tenantGuid>/<yyyy>/<mm>/<dd>/<fileGuid>
    private static readonly Regex _pattern = new(
        @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}" +
        @"/\d{4}/\d{2}/\d{2}/" +
        @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; }

    public StorageKey(string value)
    {
        if (!_pattern.IsMatch(value))
            throw new InvalidStorageKeyException(value);
        Value = value;
    }

    public static StorageKey Create(Guid tenantId, DateOnly date, Guid fileId) =>
        new($"{tenantId:D}/{date:yyyy}/{date:MM}/{date:dd}/{fileId:D}");
}
