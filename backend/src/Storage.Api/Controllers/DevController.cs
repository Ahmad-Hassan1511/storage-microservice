using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storage.Application.Abstractions;
using Storage.Domain.Enums;

namespace Storage.Api.Controllers;

/// <summary>
/// Dev-only helpers for local simulation. Not for production use.
/// </summary>
[Route("v1/dev")]
[ApiController]
[Authorize]
[ApiExplorerSettings(GroupName = "dev")]
public class DevController(IUnitOfWork unitOfWork, IHostEnvironment env) : ControllerBase
{
    [HttpPost("files/{id:guid}/mark-ready")]
    public async Task<IActionResult> MarkReady(Guid id, CancellationToken ct)
    {
        if (!env.IsDevelopment())
            return NotFound();

        Guid.TryParse(User.FindFirst("tid")?.Value, out var tenantId);

        var file = await unitOfWork.Files.GetByIdAsync(id, tenantId, ct);
        if (file is null)
            return NotFound(new { error = $"File {id} not found." });

        if (file.Status == FileStatus.Scanning)
            file.Transition(FileStatus.Ready);
        else if (file.Status != FileStatus.Ready)
            return BadRequest(new { error = $"File is in status '{file.Status}', expected Scanning or Ready." });

        file.SetVisibility(Visibility.Public);
        await unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }
}
