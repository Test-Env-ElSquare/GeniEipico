using API.Common;
using API.Contracts;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.MasterData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SkuEntity = Domain.Entities.MasterData.Sku;

namespace API.Controllers.Sku;

[Route("v1/skus")]
[Authorize]
public class SkusController : CrudController<SkuEntity, ISkuService>
{
    private readonly IFrontendDataService _frontend;
    private readonly IBillOfMaterialService _bomService;
    private readonly ISkuProcessParameterService _processParameterService;
    private readonly IMaterialService _materialService;
    private readonly IUnitOfWork _uow;

    public SkusController(
        ISkuService service,
        IFrontendDataService frontend,
        IBillOfMaterialService bomService,
        ISkuProcessParameterService processParameterService,
        IMaterialService materialService,
        IUnitOfWork uow) : base(service)
    {
        _frontend = frontend;
        _bomService = bomService;
        _processParameterService = processParameterService;
        _materialService = materialService;
        _uow = uow;
    }

    [HttpGet]
    public override Task<IActionResult> GetAll()
    {
        var customerId = GetCurrentCustomerId();
        if (!customerId.HasValue) return Task.FromResult<IActionResult>(Unauthorized(ApiResponse<object>.Fail("not_authenticated", "No authenticated customer was found.")));

        return Task.FromResult<IActionResult>(Ok(ApiResponse<object>.Ok(BuildSkuList(customerId.Value))));
    }

    [HttpGet("table")]
    public IActionResult Table(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var customerId = GetCurrentCustomerId();
        if (!customerId.HasValue) return Unauthorized(ApiResponse<object>.Fail("not_authenticated", "No authenticated customer was found."));

        return Ok(ApiResponse<object>.Ok(BuildSkuList(customerId.Value, search, category, activeOnly, page, pageSize)));
    }

    [HttpGet("dropdown")]
    public IActionResult Dropdown([FromQuery] bool activeOnly = true)
    {
        var customerId = GetCurrentCustomerId();
        if (!customerId.HasValue) return Unauthorized(ApiResponse<object>.Fail("not_authenticated", "No authenticated customer was found."));

        var query = Service.Query();
        query = query.Where(x => x.CustomerId == customerId.Value);
        if (activeOnly) query = query.Where(x => x.IsActive);

        var items = query.OrderBy(x => x.Name).Select(x => new
        {
            value = x.Id,
            label = x.Name,
            code = x.Code,
            x.Uom,
            x.StandardBatchSize
        }).ToArray();

        return Ok(ApiResponse<object>.Ok(items));
    }

    [HttpGet("form-options")]
    public IActionResult FormOptions()
    {
        var customerId = GetCurrentCustomerId();
        if (!customerId.HasValue) return Unauthorized(ApiResponse<object>.Fail("not_authenticated", "No authenticated customer was found."));

        var materialsQuery = _materialService.Query();
        materialsQuery = materialsQuery.Where(x => x.CustomerId == customerId.Value);

        var result = new
        {
            categories = Service.Query()
                .Where(x => !string.IsNullOrWhiteSpace(x.Category) && x.CustomerId == customerId.Value)
                .Select(x => x.Category!)
                .Distinct()
                .OrderBy(x => x)
                .ToArray(),
            unitsOfMeasure = new[]
            {
                new { value = "Tab", label = "Tab" },
                new { value = "Cap", label = "Cap" },
                new { value = "Kg", label = "Kg" },
                new { value = "L", label = "L" }
            },
            erpSources = new[]
            {
                new { value = true, label = "ERP / SAP" },
                new { value = false, label = "Manual" }
            },
            processParameterTemplates = new[]
            {
                new { name = "mixTime", label = "Mix Time", unit = "min", dataType = "number" },
                new { name = "pressForce", label = "Press Force", unit = "kN", dataType = "number" },
                new { name = "dryingTemp", label = "Drying Temp", unit = "C", dataType = "number" },
                new { name = "coatingThickness", label = "Coating Thickness", unit = "mm", dataType = "number" }
            },
            rawMaterials = materialsQuery
                .Where(x => x.IsActive && x.MaterialType == "RM")
                .OrderBy(x => x.Name)
                .Select(x => new { value = x.Id, label = x.Name, code = x.Code, unit = x.Uom, type = x.MaterialType })
                .ToArray()
        };

        return Ok(ApiResponse<object>.Ok(result, new { source = "db_plus_static_form_catalog" }));
    }

    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> Detail(int id)
    {
        var detail = await BuildDetailAsync(id);
        return detail is null
            ? NotFound(ApiResponse<object>.Fail("not_found", $"SKU {id} was not found."))
            : Ok(ApiResponse<object>.Ok(detail));
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateSku([FromBody] CreateSkuRequest request)
    {
        var customerId = GetCurrentCustomerId();
        if (!customerId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("not_authenticated", "The access token does not contain a valid customerId claim."));

        var validationError = await ValidateSkuRequestAsync(request, customerId.Value);
        if (validationError is not null)
            return BadRequest(ApiResponse<object>.Fail("validation_error", validationError));

        var created = await _uow.ExecuteInTransactionAsync(async () =>
        {
            var sku = ToEntity(request, customerId.Value);
            var result = await Service.CreateAsync(sku);
            await ReplaceChildrenAsync(result.Id, request.ProcessParameters, request.RawMaterials, false);
            return result;
        });

        return Created($"/v1/skus/{created.Id}/detail", ApiResponse<object>.Ok((await BuildDetailAsync(created.Id))!));
    }

    [HttpPut("{id:int}/update")]
    public async Task<IActionResult> UpdateSku(int id, [FromBody] UpdateSkuRequest request)
    {
        var customerId = GetCurrentCustomerId();
        if (!customerId.HasValue)
            return Unauthorized(ApiResponse<object>.Fail("not_authenticated", "The access token does not contain a valid customerId claim."));

        var existing = await Service.GetByIdAsync(id);
        if (existing is null || existing.CustomerId != customerId.Value)
            return NotFound(ApiResponse<object>.Fail("not_found", $"SKU {id} was not found."));

        var validationError = await ValidateSkuRequestAsync(request, customerId.Value, id);
        if (validationError is not null)
            return BadRequest(ApiResponse<object>.Fail("validation_error", validationError));

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            existing.Code = request.Code.Trim();
            existing.Name = request.Name.Trim();
            existing.Category = Clean(request.Category);
            existing.Uom = request.Uom.Trim();
            existing.StandardBatchSize = request.StandardBatchSize;
            existing.ShelfLifeMonths = request.ShelfLifeMonths;
            existing.RatedSpeedRpm = request.RatedSpeedRpm;
            existing.UnitWeightG = request.UnitWeightG;
            existing.ErpSource = request.ErpSource;
            existing.ErpMaterialCode = Clean(request.ErpMaterialCode);
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            await Service.UpdateAsync(id, existing);
            await ReplaceChildrenAsync(id, request.ProcessParameters, request.RawMaterials, true);
            return true;
        });

        return Ok(ApiResponse<object>.Ok((await BuildDetailAsync(id))!));
    }

    [HttpDelete("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var existing = await Service.GetByIdAsync(id);
        if (existing is null)
            return NotFound(ApiResponse<object>.Fail("not_found", $"SKU {id} was not found."));

        var usedByOpenBatch = _uow.Batches.Query().Any(x => x.SkuId == id &&
            x.Status != "finished" && x.Status != "cancelled" && x.Status != "split");
        if (usedByOpenBatch)
            return BadRequest(ApiResponse<object>.Fail("sku_in_use", "SKU cannot be deactivated while it is used by an open batch."));

        existing.IsActive = false;
        existing.UpdatedAt = DateTime.UtcNow;
        await Service.UpdateAsync(id, existing);
        return Ok(ApiResponse<object>.Ok(new { existing.Id, existing.Code, existing.Name, existing.IsActive }));
    }

    [HttpGet("erp-banner")]
    public async Task<IActionResult> ErpBanner()
    {
        return Ok(ApiResponse<object?>.Ok(
            await _frontend.GetSkuErpBannerAsync()
        ));
    }

    [HttpGet("{id:int}/bom")]
    public async Task<IActionResult> GetBom(int id)
    {
        return Ok(ApiResponse<object?>.Ok(
            await _frontend.GetSkuBomAsync(id)
        ));
    }

    [HttpPost("{id:int}/bom")]
    public async Task<IActionResult> AddBomLine(
        int id,
        [FromBody] CreateBomLineRequest request)
    {
        if (request.MaterialId <= 0)
            return BadRequest(ApiResponse<object>.Fail("validation_error", "MaterialId is required."));

        if (request.QtyPerBatch <= 0)
            return BadRequest(ApiResponse<object>.Fail("validation_error", "QtyPerBatch must be greater than zero."));

        if (request.TolerancePct < 0)
            return BadRequest(ApiResponse<object>.Fail("validation_error", "TolerancePct cannot be negative."));

        var entity = new BillOfMaterial
        {
            SkuId = id,
            MaterialId = request.MaterialId,
            QtyPerBatch = request.QtyPerBatch,
            TolerancePct = request.TolerancePct,
            SequenceNo = request.SequenceNo,
            IsCritical = request.IsCritical
        };

        var created = await _bomService.CreateAsync(entity);

        return Created(
            $"/v1/skus/{id}/bom/{created.Id}",
            ApiResponse<object>.Ok(new
            {
                created.Id,
                created.SkuId,
                created.MaterialId,
                created.QtyPerBatch,
                created.TolerancePct,
                created.SequenceNo,
                created.IsCritical,
                message = "BOM line added successfully."
            })
        );
    }

    [HttpPut("{id:int}/bom/{bomId:int}")]
    public async Task<IActionResult> UpdateBomLine(
        int id,
        int bomId,
        [FromBody] UpdateBomLineRequest request)
    {
        if (request.MaterialId <= 0)
            return BadRequest(ApiResponse<object>.Fail("validation_error", "MaterialId is required."));

        if (request.QtyPerBatch <= 0)
            return BadRequest(ApiResponse<object>.Fail("validation_error", "QtyPerBatch must be greater than zero."));

        if (request.TolerancePct < 0)
            return BadRequest(ApiResponse<object>.Fail("validation_error", "TolerancePct cannot be negative."));

        var existing = await _bomService.GetByIdAsync(bomId);

        if (existing is null || existing.SkuId != id)
            return NotFound(ApiResponse<object>.Fail("not_found", $"BOM line {bomId} was not found for SKU {id}."));

        existing.MaterialId = request.MaterialId;
        existing.QtyPerBatch = request.QtyPerBatch;
        existing.TolerancePct = request.TolerancePct;
        existing.SequenceNo = request.SequenceNo;
        existing.IsCritical = request.IsCritical;
        existing.UpdatedAt = DateTime.UtcNow;

        var updated = await _bomService.UpdateAsync(bomId, existing);

        return Ok(ApiResponse<object>.Ok(new
        {
            updated!.Id,
            updated.SkuId,
            updated.MaterialId,
            updated.QtyPerBatch,
            updated.TolerancePct,
            updated.SequenceNo,
            updated.IsCritical,
            message = "BOM line updated successfully."
        }));
    }

    [HttpDelete("{id:int}/bom/{bomId:int}")]
    public async Task<IActionResult> DeleteBomLine(
        int id,
        int bomId)
    {
        var existing = await _bomService.GetByIdAsync(bomId);

        if (existing is null || existing.SkuId != id)
            return NotFound(ApiResponse<object>.Fail("not_found", $"BOM line {bomId} was not found for SKU {id}."));

        var deleted = await _bomService.DeleteAsync(bomId);

        return deleted
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail("not_found", $"BOM line {bomId} was not found."));
    }

    private async Task<string?> ValidateSkuRequestAsync(
        SkuRequestBase request,
        int customerId,
        int? currentId = null)
    {
        var customer = await _uow.Customers.GetByIdAsync(customerId, true);
        if (customer is null || !customer.IsActive || customer.IsDeleted)
            return "The authenticated customer was not found or is inactive.";
        if (string.IsNullOrWhiteSpace(request.Code)) return "SKU code is required.";
        if (string.IsNullOrWhiteSpace(request.Name)) return "Product name is required.";
        if (string.IsNullOrWhiteSpace(request.Uom)) return "Unit of measure is required.";
        if (request.StandardBatchSize <= 0) return "StandardBatchSize must be greater than zero.";
        if (request.ShelfLifeMonths is <= 0) return "ShelfLifeMonths must be greater than zero when supplied.";
        if (request.RatedSpeedRpm is <= 0) return "RatedSpeedRpm must be greater than zero when supplied.";
        if (request.UnitWeightG is <= 0) return "UnitWeightG must be greater than zero when supplied.";

        var code = request.Code.Trim();
        if (Service.Query().Any(x => x.CustomerId == customerId && x.Code == code && x.Id != currentId))
            return $"SKU code '{code}' already exists for this customer.";

        if (request.ProcessParameters is not null)
        {
            if (request.ProcessParameters.Any(x => string.IsNullOrWhiteSpace(x.Name) || string.IsNullOrWhiteSpace(x.Value)))
                return "Every process parameter requires Name and Value.";
            if (request.ProcessParameters.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                return "Process parameter names must be unique within the SKU.";
        }

        if (request.RawMaterials is not null)
        {
            if (request.RawMaterials.Any(x => x.MaterialId <= 0 || x.QtyPerBatch <= 0 || x.SequenceNo <= 0))
                return "Every raw material requires a valid MaterialId and positive QtyPerBatch/SequenceNo.";
            if (request.RawMaterials.GroupBy(x => x.MaterialId).Any(x => x.Count() > 1))
                return "A material cannot appear more than once in the same BOM.";

            var materialIds = request.RawMaterials.Select(x => x.MaterialId).ToHashSet();
            var validMaterialIds = _materialService.Query().Where(x => materialIds.Contains(x.Id) && x.CustomerId == customerId && x.IsActive).Select(x => x.Id).ToHashSet();
            if (!materialIds.SetEquals(validMaterialIds))
                return "One or more raw materials do not exist, are inactive, or belong to another customer.";
        }

        return null;
    }

    private object BuildSkuList(
        int customerId,
        string? search = null,
        string? category = null,
        bool activeOnly = false,
        int page = 1,
        int pageSize = 10)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);

        var skus = Service.Query();
        skus = skus.Where(x => x.CustomerId == customerId);
        if (activeOnly) skus = skus.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(category)) skus = skus.Where(x => x.Category == category.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            skus = skus.Where(x => x.Code.Contains(term) || x.Name.Contains(term));
        }

        var boms = _bomService.Query().ToList();
        var parameters = _processParameterService.Query().ToList();

        var skuRows = skus
            .OrderBy(x => x.Code)
            .ToList();

        var rows = skuRows
            .Select(s => new
            {
                id = s.Id,
                customerId = s.CustomerId,
                skuCode = s.Code,
                productName = s.Name,
                category = s.Category,
                unitOfMeasure = s.Uom,
                standardBatchSize = s.StandardBatchSize,
                shelfLifeMonths = s.ShelfLifeMonths,
                ratedSpeedRpm = s.RatedSpeedRpm,
                unitWeightG = s.UnitWeightG,
                erpSource = s.ErpSource,
                erpMaterialCode = s.ErpMaterialCode,
                lastErpSyncAt = s.LastErpSyncAt,
                isActive = s.IsActive,
                rawMaterialsCount = boms.Count(b => b.SkuId == s.Id),
                mesParameters = parameters
                    .Where(p => p.SkuId == s.Id)
                    .OrderBy(p => p.Name)
                    .Select(p => new { p.Name, p.Value, p.Unit, p.DataType })
                    .ToArray()
            })
            .ToArray();

        var total = rows.Length;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        return new
        {
            items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToArray<object>(),
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages,
                hasPrevious = page > 1,
                hasNext = totalPages > 0 && page < totalPages
            },
            filters = new
            {
                search,
                category,
                customerId,
                activeOnly
            }
        };
    }

    private async Task ReplaceChildrenAsync(int skuId, IReadOnlyList<SkuProcessParameterRequest>? parameters, IReadOnlyList<SkuRawMaterialRequest>? materials, bool replace)
    {
        if (parameters is not null)
        {
            if (replace)
                foreach (var row in _processParameterService.Query().Where(x => x.SkuId == skuId).ToArray())
                    await _processParameterService.DeleteAsync(row.Id);

            foreach (var item in parameters)
                await _processParameterService.CreateAsync(new SkuProcessParameter
                {
                    SkuId = skuId, Name = item.Name.Trim(), Value = item.Value.Trim(),
                    Unit = Clean(item.Unit), DataType = string.IsNullOrWhiteSpace(item.DataType) ? "number" : item.DataType.Trim()
                });
        }

        if (materials is not null)
        {
            if (replace)
                foreach (var row in _bomService.Query().Where(x => x.SkuId == skuId).ToArray())
                    await _bomService.DeleteAsync(row.Id);

            foreach (var item in materials)
                await _bomService.CreateAsync(new BillOfMaterial
                {
                    SkuId = skuId, MaterialId = item.MaterialId, QtyPerBatch = item.QtyPerBatch,
                    TolerancePct = 2m, SequenceNo = item.SequenceNo, IsCritical = item.IsCritical
                });
        }
    }

    private async Task<object?> BuildDetailAsync(int id)
    {
        var sku = await Service.GetByIdAsync(id);
        if (sku is null) return null;
        var materialLookup = _materialService.Query().ToDictionary(x => x.Id);
        return new
        {
            sku.Id, sku.CustomerId, sku.Code, sku.Name, sku.Category, sku.Uom,
            sku.StandardBatchSize, sku.ShelfLifeMonths, sku.RatedSpeedRpm, sku.UnitWeightG,
            sku.ErpSource, sku.ErpMaterialCode, sku.LastErpSyncAt, sku.IsActive,
            processParameters = _processParameterService.Query().Where(x => x.SkuId == id).OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.Value, x.Unit, x.DataType }).ToArray(),
            rawMaterials = _bomService.Query().Where(x => x.SkuId == id).OrderBy(x => x.SequenceNo).ToArray()
                .Select(x => new { x.Id, x.MaterialId, materialCode = materialLookup.GetValueOrDefault(x.MaterialId)?.Code, materialName = materialLookup.GetValueOrDefault(x.MaterialId)?.Name, unit = materialLookup.GetValueOrDefault(x.MaterialId)?.Uom, x.QtyPerBatch, x.TolerancePct, x.SequenceNo, x.IsCritical }).ToArray()
        };
    }

    private static SkuEntity ToEntity(SkuRequestBase request, int customerId) => new()
    {
        CustomerId = customerId, Code = request.Code.Trim(), Name = request.Name.Trim(),
        Category = Clean(request.Category), Uom = request.Uom.Trim(), StandardBatchSize = request.StandardBatchSize,
        ShelfLifeMonths = request.ShelfLifeMonths, RatedSpeedRpm = request.RatedSpeedRpm,
        UnitWeightG = request.UnitWeightG, ErpSource = request.ErpSource,
        ErpMaterialCode = Clean(request.ErpMaterialCode), IsActive = request.IsActive
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private int? GetCurrentCustomerId()
    {
        var idValue = User.FindFirstValue("customerId");
        return int.TryParse(idValue, out var customerId) && customerId > 0 ? customerId : null;
    }
}
