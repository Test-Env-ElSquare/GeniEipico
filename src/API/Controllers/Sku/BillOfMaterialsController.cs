using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Domain.Entities.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Sku;

[Route("v1/bill-of-materials")]
public class BillOfMaterialsController : CrudController<BillOfMaterial, IBillOfMaterialService>
{
    private readonly IFrontendDataService _frontend;
    private readonly IBillOfMaterialService _bomService;

    public BillOfMaterialsController(
        IBillOfMaterialService service,
        IFrontendDataService frontend) : base(service)
    {
        _bomService = service;
        _frontend = frontend;
    }

    // Endpoint Type A/D:
    // General BOM table/list DTO.
    // Use Case: UC-MD03 Manage BOM.
    [ApiExplorerSettings(IgnoreApi = false)]
    [HttpGet("table")]
    public async Task<IActionResult> GetTable()
    {
        return Ok(ApiResponse<object?>.Ok(
            await _frontend.GetBomListAsync()
        ));
    }

    // Endpoint Type A/D:
    // Single BOM line detail DTO.
    // Use Case: UC-MD03 Manage BOM.
    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> GetDetail(
        int id)
    {
        var bom = await _bomService.GetByIdAsync(id);

        if (bom is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"BOM line {id} was not found."
            ));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            bom.Id,
            bom.SkuId,
            bom.MaterialId,
            bom.QtyPerBatch,
            bom.TolerancePct,
            bom.SequenceNo,
            bom.IsCritical
        }));
    }

    // Endpoint Type A now / Type B later:
    // Create BOM line.
    // Later: BOM versioning, approval, active-BOM edit protection.
    // Use Case: UC-MD03 Manage BOM.
    [HttpPost("create")]
    public async Task<IActionResult> CreateBomLine(
        [FromBody] CreateBillOfMaterialRequest request)
    {
        var validationError = ValidateBomRequest(request);

        if (validationError is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "validation_error",
                validationError
            ));
        }

        var entity = new BillOfMaterial
        {
            SkuId = request.SkuId,
            MaterialId = request.MaterialId,
            QtyPerBatch = request.QtyPerBatch,
            TolerancePct = request.TolerancePct,
            SequenceNo = request.SequenceNo,
            IsCritical = request.IsCritical
        };

        var created = await _bomService.CreateAsync(entity);

        return Created(
            $"/v1/bill-of-materials/{created.Id}/detail",
            ApiResponse<object>.Ok(new
            {
                created.Id,
                created.SkuId,
                created.MaterialId,
                created.QtyPerBatch,
                created.TolerancePct,
                created.SequenceNo,
                created.IsCritical,
                message = "BOM line created successfully."
            })
        );
    }

    // Endpoint Type A now / Type B later:
    // Update BOM line.
    // Later: block update if BOM is active/used by batches.
    // Use Case: UC-MD03 Manage BOM.
    [HttpPut("{id:int}/update")]
    public async Task<IActionResult> UpdateBomLine(
        int id,
        [FromBody] UpdateBillOfMaterialRequest request)
    {
        var validationError = ValidateBomRequest(request);

        if (validationError is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "validation_error",
                validationError
            ));
        }

        var existing = await _bomService.GetByIdAsync(id);

        if (existing is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"BOM line {id} was not found."
            ));
        }

        existing.SkuId = request.SkuId;
        existing.MaterialId = request.MaterialId;
        existing.QtyPerBatch = request.QtyPerBatch;
        existing.TolerancePct = request.TolerancePct;
        existing.SequenceNo = request.SequenceNo;
        existing.IsCritical = request.IsCritical;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _bomService.UpdateAsync(id, existing);

        if (updated is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"BOM line {id} was not found."
            ));
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            updated.Id,
            updated.SkuId,
            updated.MaterialId,
            updated.QtyPerBatch,
            updated.TolerancePct,
            updated.SequenceNo,
            updated.IsCritical,
            message = "BOM line updated successfully."
        }));
    }

    // Endpoint Type A/B:
    // Delete/deactivate BOM line.
    // Later: prefer archive instead of hard delete if used by batches.
    // Use Case: UC-MD03 Manage BOM.
    [HttpDelete("{id:int}/delete")]
    public async Task<IActionResult> DeleteBomLine(
        int id)
    {
        var existing = await _bomService.GetByIdAsync(id);

        if (existing is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"BOM line {id} was not found."
            ));
        }

        var deleted = await _bomService.DeleteAsync(id);

        return deleted
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(
                "not_found",
                $"BOM line {id} was not found."
            ));
    }

    private static string? ValidateBomRequest(BillOfMaterialRequestBase request)
    {
        if (request.SkuId <= 0)
            return "SkuId is required.";

        if (request.MaterialId <= 0)
            return "MaterialId is required.";

        if (request.QtyPerBatch <= 0)
            return "QtyPerBatch must be greater than zero.";

        if (request.TolerancePct < 0)
            return "TolerancePct cannot be negative.";

        if (request.SequenceNo <= 0)
            return "SequenceNo must be greater than zero.";

        return null;
    }
}