using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;
using MaterialEntity = Domain.Entities.MasterData.Material;

namespace API.Controllers.Sku;

[Route("v1/materials")]
public class MaterialsController : CrudController<MaterialEntity, IMaterialService>
{
    private readonly IFrontendDataService _frontend;
    private readonly IMaterialService _materialService;

    public MaterialsController(
        IMaterialService service,
        IFrontendDataService frontend) : base(service)
    {
        _materialService = service;
        _frontend = frontend;
    }

    // Endpoint Type A/D:
    // Master Data read model for Materials list.
    // Frontend binds here for Materials table / BOM material dropdown.
    // Use Case: UC-MD02 Manage Materials.
    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        return Ok(ApiResponse<object?>.Ok(
            await _frontend.GetMaterialListAsync()
        ));
    }

    [HttpGet("dropdown")]
    public IActionResult Dropdown(
        [FromQuery] int? customerId = null,
        [FromQuery] string? materialType = "RM",
        [FromQuery] bool activeOnly = true)
    {
        var query = _materialService.Query();
        if (customerId.HasValue) query = query.Where(x => x.CustomerId == customerId.Value);
        if (!string.IsNullOrWhiteSpace(materialType)) query = query.Where(x => x.MaterialType == materialType);
        if (activeOnly) query = query.Where(x => x.IsActive);

        var items = query.OrderBy(x => x.Name).Select(x => new
        {
            value = x.Id,
            label = x.Name,
            code = x.Code,
            unit = x.Uom,
            type = x.MaterialType
        }).ToArray();

        return Ok(ApiResponse<object>.Ok(items));
    }

    // Endpoint Type A/D:
    // Material detail DTO, not raw entity navigation graph.
    // Use Case: UC-MD02 Manage Materials.
    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> GetDetail(
        int id)
    {
        var material = await _materialService.GetByIdAsync(id);

        if (material is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"Material {id} was not found."
            ));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            material.Id,
            material.CustomerId,
            material.Code,
            material.Name,
            material.MaterialType,
            material.Uom,
            material.ErpCode,
            material.SupplierCode,
            material.ReorderPointKg,
            material.MinStockKg,
            material.IsActive
        }));
    }

    // Endpoint Type A now / Type B later:
    // Create Material with validation.
    // Later: unique code, ERP validation, audit log.
    // Use Case: UC-MD02 Manage Materials.
    [HttpPost("create")]
    public async Task<IActionResult> CreateMaterial(
        [FromBody] CreateMaterialRequest request)
    {
        var validationError = ValidateMaterialRequest(request);

        if (validationError is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "validation_error",
                validationError
            ));
        }

        var entity = new MaterialEntity
        {
            CustomerId = request.CustomerId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            MaterialType = request.MaterialType.Trim(),
            Uom = request.Uom.Trim(),
            ErpCode = string.IsNullOrWhiteSpace(request.ErpCode) ? null : request.ErpCode.Trim(),
            SupplierCode = string.IsNullOrWhiteSpace(request.SupplierCode) ? null : request.SupplierCode.Trim(),
            ReorderPointKg = request.ReorderPointKg,
            MinStockKg = request.MinStockKg,
            IsActive = request.IsActive
        };

        var created = await _materialService.CreateAsync(entity);

        return Created(
            $"/v1/materials/{created.Id}/detail",
            ApiResponse<object>.Ok(new
            {
                created.Id,
                created.CustomerId,
                created.Code,
                created.Name,
                created.MaterialType,
                created.Uom,
                created.ErpCode,
                created.SupplierCode,
                created.ReorderPointKg,
                created.MinStockKg,
                created.IsActive,
                message = "Material created successfully."
            })
        );
    }

    // Endpoint Type A now / Type B later:
    // Update Material metadata.
    // Later: block UOM change after material is used in BOM/batches.
    // Use Case: UC-MD02 Manage Materials.
    [HttpPut("{id:int}/update")]
    public async Task<IActionResult> UpdateMaterial(
        int id,
        [FromBody] UpdateMaterialRequest request)
    {
        var validationError = ValidateMaterialRequest(request);

        if (validationError is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "validation_error",
                validationError
            ));
        }

        var existing = await _materialService.GetByIdAsync(id);

        if (existing is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"Material {id} was not found."
            ));
        }

        existing.CustomerId = request.CustomerId;
        existing.Code = request.Code.Trim();
        existing.Name = request.Name.Trim();
        existing.MaterialType = request.MaterialType.Trim();
        existing.Uom = request.Uom.Trim();
        existing.ErpCode = string.IsNullOrWhiteSpace(request.ErpCode) ? null : request.ErpCode.Trim();
        existing.SupplierCode = string.IsNullOrWhiteSpace(request.SupplierCode) ? null : request.SupplierCode.Trim();
        existing.ReorderPointKg = request.ReorderPointKg;
        existing.MinStockKg = request.MinStockKg;
        existing.IsActive = request.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _materialService.UpdateAsync(id, existing);

        if (updated is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"Material {id} was not found."
            ));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            updated.Id,
            updated.CustomerId,
            updated.Code,
            updated.Name,
            updated.MaterialType,
            updated.Uom,
            updated.ErpCode,
            updated.SupplierCode,
            updated.ReorderPointKg,
            updated.MinStockKg,
            updated.IsActive,
            message = "Material updated successfully."
        }));
    }

    // Endpoint Type A/B:
    // Prefer deactivate instead of hard delete for master data.
    // Later: block deactivate if active BOM/batch references exist.
    // Use Case: UC-MD02 Manage Materials.
    [HttpDelete("{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateMaterial(
        int id)
    {
        var existing = await _materialService.GetByIdAsync(id);

        if (existing is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"Material {id} was not found."
            ));
        }

        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _materialService.UpdateAsync(id, existing);

        if (updated is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"Material {id} was not found."
            ));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            updated.Id,
            updated.Code,
            updated.Name,
            updated.IsActive,
            message = "Material deactivated successfully."
        }));
    }

    private static string? ValidateMaterialRequest(MaterialRequestBase request)
    {
        if (request.CustomerId <= 0)
            return "CustomerId is required.";

        if (string.IsNullOrWhiteSpace(request.Code))
            return "Material code is required.";

        if (string.IsNullOrWhiteSpace(request.Name))
            return "Material name is required.";

        if (string.IsNullOrWhiteSpace(request.MaterialType))
            return "Material type is required.";

        if (string.IsNullOrWhiteSpace(request.Uom))
            return "UOM is required.";

        if (request.ReorderPointKg < 0)
            return "ReorderPointKg cannot be negative.";

        if (request.MinStockKg < 0)
            return "MinStockKg cannot be negative.";

        return null;
    }
}
