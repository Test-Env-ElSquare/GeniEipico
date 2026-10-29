using API.Common;
using Application.Abstractions.Services;
using Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace API.Common;

[ApiController]
public abstract class CrudController<TEntity, TService> : ControllerBase
    where TEntity : Entity
    where TService : ICrudService<TEntity>
{
    protected readonly TService Service;

    protected CrudController(TService service)
    {
        Service = service;
    }

    // Endpoint Type A: CRUD / Direct DB Query. Generic list endpoint for entity management.
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet]
    public virtual async Task<IActionResult> GetAll()
    {
        var items = await Service.GetAllAsync();
        return Ok(ApiResponse<IReadOnlyList<TEntity>>.Ok(items));
    }

    // Endpoint Type A: CRUD / Direct DB Query. Generic get-by-id endpoint for entity management.
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpGet("{id:int}")]
    public virtual async Task<IActionResult> GetById(int id)
    {
        var entity = await Service.GetByIdAsync(id);
        if (entity is null)
        {
            return NotFound(ApiResponse<TEntity>.Fail("not_found", $"{typeof(TEntity).Name} with id {id} was not found."));
        }

        return Ok(ApiResponse<TEntity>.Ok(entity));
    }

    // Endpoint Type A: CRUD / Direct DB Query. Generic create endpoint; workflow-specific creates should override this.
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPost]
    public virtual async Task<IActionResult> Create([FromBody] TEntity entity)
    {
        var created = await Service.CreateAsync(entity);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse<TEntity>.Ok(created));
    }

    // Endpoint Type A: CRUD / Direct DB Query. Generic update endpoint; workflow-specific updates should use Application Services.
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpPut("{id:int}")]
    public virtual async Task<IActionResult> Update(int id, [FromBody] TEntity entity)
    {
        var updated = await Service.UpdateAsync(id, entity);
        if (updated is null)
        {
            return NotFound(ApiResponse<TEntity>.Fail("not_found", $"{typeof(TEntity).Name} with id {id} was not found."));
        }

        return Ok(ApiResponse<TEntity>.Ok(updated));
    }

    // Endpoint Type A: CRUD / Direct DB Query. Generic delete endpoint; production entities should move to soft-delete/cancel later.
    [ApiExplorerSettings(IgnoreApi = true)]
    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id)
    {
        var deleted = await Service.DeleteAsync(id);
        if (!deleted)
        {
            return NotFound(ApiResponse<bool>.Fail("not_found", $"{typeof(TEntity).Name} with id {id} was not found."));
        }

        return NoContent();
    }

    protected IActionResult StaticOk(string endpoint, object? parameters = null, string? message = null)
    {
        var result = new StaticEndpointResult(
            endpoint,
            message ?? "Endpoint route is ready. Connect domain/service logic when the use-case is implemented.",
            parameters);

        return Ok(ApiResponse<StaticEndpointResult>.Ok(result));
    }
}
