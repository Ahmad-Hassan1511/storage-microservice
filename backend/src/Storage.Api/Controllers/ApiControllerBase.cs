using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storage.Application.Common;

namespace Storage.Api.Controllers;

[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected CallerContext BuildCaller()
    {
        var user = User;
        Guid.TryParse(user.FindFirst("tid")?.Value, out var tenantId);
        var principalId = user.FindFirst("sub")?.Value ?? user.FindFirst("oid")?.Value ?? string.Empty;
        var principalType = user.FindFirst("azp") is not null ? "service" : "user";
        var scopes = (user.FindFirst("scp")?.Value ?? user.FindFirst("roles")?.Value ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new CallerContext(tenantId, principalType, principalId, scopes);
    }

    protected IActionResult MapError(ApplicationError error) => error switch
    {
        NotFoundError e            => NotFound(new { error = e.Message }),
        AccessDeniedError          => StatusCode(403),
        IdempotencyConflictError e => Conflict(new { error = e.Message }),
        ChecksumMismatchError e    => UnprocessableEntity(new { error = e.Message }),
        PolicyViolationError e     => Problem(e.Message, statusCode: e.HttpStatusHint),
        _                          => Problem("An unexpected error occurred.", statusCode: 500),
    };
}
