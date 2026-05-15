using Storage.Domain.Enums;

namespace Storage.Domain.Common;

public abstract class DomainException(string message) : Exception(message);

public sealed class InvalidStatusTransitionException(FileStatus from, FileStatus to)
    : DomainException($"Cannot transition from {from} to {to}.");

public sealed class InvalidStorageKeyException(string key)
    : DomainException($"'{key}' is not a valid storage key.");

public sealed class InvalidChecksumException(string value)
    : DomainException($"'{value}' is not a valid SHA-256 checksum.");
