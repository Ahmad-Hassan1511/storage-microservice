using Microsoft.AspNetCore.Mvc;
using Storage.Application.Abstractions;

namespace Storage.Api.Controllers;

[Route("v1/categories")]
public class CategoriesController(IUnitOfWork unitOfWork) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var categories = await unitOfWork.Categories.ListAllAsync(ct);
        return Ok(categories);
    }
}
