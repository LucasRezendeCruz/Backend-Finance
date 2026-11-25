using Finance.Application.Categories;
using Finance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
[EnableRateLimiting("api-authenticated")]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    public sealed record CreateCategoryRequest(
        string Name,
        CategoryType Type,
        string? Icon = null,
        string? Color = null);

    public sealed record UpdateCategoryRequest(
        string Name,
        string? Icon = null,
        string? Color = null);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type = null,
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default)
    {
        CategoryType? parsedType = ControllerQueryHelper.ParseOptionalEnum<CategoryType>(type, nameof(type));
        return Ok(await categoryService.GetAllAsync(parsedType, includeArchived, ct));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken ct = default)
    {
        CategoryDto created = await categoryService.CreateAsync(
            new CreateCategoryCommand(request.Name, request.Type, request.Icon, request.Color),
            ct);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken ct = default)
        => Ok(await categoryService.UpdateAsync(
            id,
            new UpdateCategoryCommand(request.Name, request.Icon, request.Color),
            ct));

    [HttpPatch("{id:guid}/archive")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(
        Guid id,
        [FromBody] ArchiveRequest? request = null,
        CancellationToken ct = default)
        => Ok(await categoryService.SetArchivedAsync(id, request?.IsArchived ?? true, ct));
}
