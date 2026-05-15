namespace Storage.Application.Common;

public sealed record CallerContext(
    Guid TenantId,
    string PrincipalType,   // "service" | "user"
    string PrincipalId,
    IReadOnlyList<string> Scopes);
