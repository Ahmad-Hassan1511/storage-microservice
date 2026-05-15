using Storage.Domain.Common;

namespace Storage.Domain.Entities;

public class FileTag : EntityBase
{
    public Guid FileId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
}
