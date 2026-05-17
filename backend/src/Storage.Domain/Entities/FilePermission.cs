using Storage.Domain.Common;
using Storage.Domain.Enums;

namespace Storage.Domain.Entities;

public class FilePermission : EntityBase
{
    public Guid FileId { get; private set; }
    public string PrincipalType { get; private set; } = string.Empty;
    public string PrincipalId { get; private set; } = string.Empty;
    public Permission Permission { get; private set; }

    private FilePermission() { }

    public static FilePermission Create(Guid fileId, string principalType, string principalId, Permission permission) =>
        new()
        {
            Id = Guid.NewGuid(),
            FileId = fileId,
            PrincipalType = principalType,
            PrincipalId = principalId,
            Permission = permission
        };
}
