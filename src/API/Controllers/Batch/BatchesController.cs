using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using BatchEntity = Domain.Entities.Production.Batch;

namespace API.Controllers.Batch;

[Authorize]
[Route("v1/batches")]
public class BatchesController : CrudController<BatchEntity, IBatchService>
{
    private readonly IFrontendDataService _frontend;
    private readonly IBatchWorkflowService _workflow;
    private readonly ITimeUtilities _timeUtilities;
    private readonly IFactoryService _factories;
    private readonly IProductionLineService _lines;
    private readonly ISkuService _skus;
    private readonly IShiftService _shifts;

    public BatchesController(
        IBatchService service,
        IFrontendDataService frontend,
        IBatchWorkflowService workflow,
        ITimeUtilities timeUtilities,
        IFactoryService factories,
        IProductionLineService lines,
        ISkuService skus,
        IShiftService shifts) : base(service)
    {
        _frontend = frontend;
        _workflow = workflow;
        _timeUtilities = timeUtilities;
        _factories = factories;
        _lines = lines;
        _skus = skus;
        _shifts = shifts;
    }

    [HttpPost("manual")]
    public async Task<IActionResult> CreateManual([FromBody] CreateBatchRequest request)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (request.FactoryId <= 0)
            return BadRequest(ApiResponse<object>.Fail("factory_required", "FactoryId must be greater than zero."));
        if (request.LineId <= 0)
            return BadRequest(ApiResponse<object>.Fail("line_required", "LineId must be greater than zero."));

        var selectionError = ValidateRequestedScope(scope, request.FactoryId, request.LineId, out _);
        if (selectionError is not null) return selectionError;

        var result = await _workflow.CreateManualBatchAsync(
            scope.CustomerId, request.SkuId, request.FactoryId, request.LineId, request.PlannedQty,
            request.PlannedStart, request.PlannedEnd, request.ShiftId, request.Priority, request.Notes);
        return this.ToActionResult(result);
    }

    [HttpGet("form-options")]
    public IActionResult FormOptions([FromQuery] int? factoryId = null)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;

        var selectionError = ValidateRequestedScope(scope, factoryId, null, out var allowedLineIds);
        if (selectionError is not null) return selectionError;

        var factories = _factories.Query()
            .Where(x => scope.FactoryIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToList();
        var lines = _lines.Query()
            .Where(x => allowedLineIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.FactoryId, x.Code, x.Name })
            .ToList();
        var skus = _skus.Query()
            .Where(x => x.CustomerId == scope.CustomerId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, code = x.Code, name = x.Name, x.StandardBatchSize })
            .ToList();
        var shiftFactoryIds = factoryId.HasValue
            ? new[] { factoryId.Value }
            : scope.FactoryIds.ToArray();
        var shifts = _shifts.Query()
            .Where(x => x.IsActive &&
                        !x.IsDeleted &&
                        (shiftFactoryIds.Contains(x.FactoryId) ||
                         x.FactoryLinks.Any(link => shiftFactoryIds.Contains(link.FactoryId))))
            .OrderBy(x => x.StartTime)
            .Select(x => new { x.Id, x.FactoryId, x.Name, x.StartTime, x.EndTime })
            .ToList();

        return Ok(ApiResponse<object>.Ok(new
        {
            factories,
            lines,
            skus,
            shifts,
            priorities = new[]
            {
                new { value = (int)BatchPriority.Low, label = "Low" },
                new { value = (int)BatchPriority.Normal, label = "Normal" },
                new { value = (int)BatchPriority.High, label = "High" }
            },
            defaultPriority = (int)BatchPriority.Normal
        }));
    }

    [HttpGet("statuses")]
    public IActionResult Statuses()
    {
        var result = new
        {
            defaultStatus = "planned",
            statuses = CreateStatusOptions()
        };

        return Ok(ApiResponse<object>.Ok(result));
    }

    [HttpGet("stages")]
    public IActionResult Stages()
    {
        var result = new
        {
            defaultStage = "warehouse",
            stages = new[]
            {
                new { value = "warehouse", label = "Warehouse", order = 1, allowsDispense = false, isFinal = false },
                new { value = "dispense", label = "Dispense", order = 2, allowsDispense = true, isFinal = false },
                new { value = "production", label = "Production", order = 3, allowsDispense = false, isFinal = false },
                new { value = "finished", label = "Finished", order = 4, allowsDispense = false, isFinal = true }
            },
            note = "Batch stages are internal workflow steps. UI board columns should use batch statuses."
        };

        return Ok(ApiResponse<object>.Ok(result, new { source = "static_workflow_catalog" }));
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        var filter = await ResolveFilterAsync(new FrontendFilterQuery(), scope, scope.LineIds);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchTableAsync(filter), filter));
    }

    [HttpGet("table")]
    public async Task<IActionResult> Table([FromQuery] FrontendFilterQuery query)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        var selectionError = ValidateRequestedScope(scope, query.FactoryId, query.LineId, out var allowedLineIds);
        if (selectionError is not null) return selectionError;
        var filter = await ResolveFilterAsync(query, scope, allowedLineIds);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchTableAsync(filter), filter));
    }

    [HttpGet("{id:int}")]
    public override async Task<IActionResult> GetById(int id)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        var detail = await _frontend.GetBatchDetailDtoAsync(id, CreateAccessOnlyFilter(scope));
        return detail is null
            ? NotFound(ApiResponse<object>.Fail("not_found", $"Batch {id} was not found."))
            : Ok(ApiResponse<object?>.Ok(detail));
    }

    [HttpGet("{id:int}/detail")]
    public async Task<IActionResult> Detail(int id)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        var detail = await _frontend.GetBatchDetailDtoAsync(id, CreateAccessOnlyFilter(scope));
        return detail is null
            ? NotFound(ApiResponse<object>.Fail("not_found", $"Batch {id} was not found."))
            : Ok(ApiResponse<object?>.Ok(detail));
    }

    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> Start(int id, [FromBody] StartBatchRequest request)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        var result = await _workflow.StartBatchAsync(id, request.Notes);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] CompleteBatchRequest request)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        var result = await _workflow.CompleteBatchAsync(id, request.ProducedQty, request.RejectedQty, request.Notes);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:int}/advance-stage")]
    public async Task<IActionResult> AdvanceStage(int id, [FromBody] AdvanceBatchStageRequest request)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        var result = await _workflow.AdvanceStageAsync(id, request.Notes);
        return this.ToActionResult(result);
    }

    [HttpPost("{id:int}/split")]
    public async Task<IActionResult> Split(int id, [FromBody] SplitBatchRequest request)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        var result = await _workflow.SplitBatchAsync(id, request.SplitQty, request.Reason);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}/materials")]
    public async Task<IActionResult> GetMaterials(int id)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        return Ok(ApiResponse<object>.Ok(await _frontend.GetBatchMaterialsAsync(id)));
    }

    [HttpPost("{id:int}/materials/{materialId:int}/dispense")]
    public async Task<IActionResult> DispenseMaterial(int id, int materialId, [FromBody] DispenseMaterialRequest request)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        var result = await _workflow.RecordManualDispenseAsync(id, materialId, request.ScaleId, request.DispensedQty, request.Timestamp);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}/stage-log")]
    public async Task<IActionResult> GetStageLog(int id)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        return Ok(ApiResponse<object>.Ok(await _frontend.GetBatchStageLogAsync(id)));
    }

    [HttpGet("by-uid/{uid}")]
    public async Task<IActionResult> GetByUid(string uid)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        var batch = Service.Query().FirstOrDefault(x =>
            x.Uid == uid &&
            x.CustomerId == scope.CustomerId &&
            scope.LineIds.Contains(x.LineId));
        if (batch is null)
        {
            return NotFound(ApiResponse<object>.Fail("not_found", $"Batch with UID {uid} was not found."));
        }

        return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchDetailDtoAsync(batch.Id, CreateAccessOnlyFilter(scope))));
    }

    [HttpGet("kanban")]
    public async Task<IActionResult> Kanban([FromQuery] FrontendFilterQuery query)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        var selectionError = ValidateRequestedScope(scope, query.FactoryId, query.LineId, out var allowedLineIds);
        if (selectionError is not null) return selectionError;
        var filter = await ResolveFilterAsync(query, scope, allowedLineIds);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchKanbanAsync(filter), filter));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] FrontendFilterQuery query)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        var selectionError = ValidateRequestedScope(scope, query.FactoryId, query.LineId, out var allowedLineIds);
        if (selectionError is not null) return selectionError;
        var filter = await ResolveHistoryFilterAsync(query, scope, allowedLineIds);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetBatchHistoryAsync(filter), filter));
    }

    [HttpGet("{id:int}/history-detail")]
    public async Task<IActionResult> HistoryDetail(int id)
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        if (!CanAccessBatch(id, scope)) return BatchNotFound(id);
        var detail = await _frontend.GetBatchHistoryDetailAsync(id, CreateAccessOnlyFilter(scope));
        return detail is null
            ? NotFound(ApiResponse<object>.Fail("not_found", $"Batch {id} was not found."))
            : Ok(ApiResponse<object?>.Ok(detail));
    }

    [HttpGet("preview-uid")]
    public async Task<IActionResult> PreviewUid()
    {
        if (!TryResolveAccessScope(out var scope, out var accessError)) return accessError;
        var preview = await _workflow.GetPreviewBatchUidAsync(scope.CustomerId);
        return Ok(ApiResponse<string>.Ok(preview));
    }

    [NonAction]
    public override Task<IActionResult> Create(BatchEntity entity) => base.Create(entity);

    [NonAction]
    public override Task<IActionResult> Update(int id, BatchEntity entity) => base.Update(id, entity);

    [NonAction]
    public override Task<IActionResult> Delete(int id) => base.Delete(id);

    private async Task<FrontendFilter> ResolveFilterAsync(
        FrontendFilterQuery query,
        BatchAccessScope scope,
        IReadOnlyCollection<int> allowedLineIds)
    {
        var resolved = FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
        return resolved with
        {
            Search = query.Search,
            Status = query.Status,
            Page = SafePage(query.Page),
            PageSize = SafePageSize(query.PageSize),
            CustomerId = scope.CustomerId,
            AllowedLineIds = allowedLineIds
        };
    }

    private async Task<FrontendFilter> ResolveHistoryFilterAsync(
        FrontendFilterQuery query,
        BatchAccessScope scope,
        IReadOnlyCollection<int> allowedLineIds)
    {
        var isAllHistory =
            !query.HasExplicitTimeFilter() ||
            query.Duration is 0 or 5;

        if (isAllHistory)
        {
            return new FrontendFilter(
                query.Duration ?? 5,
                query.FactoryId,
                query.LineId,
                DateTime.MinValue,
                DateTime.MaxValue,
                null,
                null,
                "all",
                "All historical batches. No time window is applied.",
                query.Search,
                query.Status,
                SafePage(query.Page),
                SafePageSize(query.PageSize),
                ApplyTimeFilter: false,
                CustomerId: scope.CustomerId,
                AllowedLineIds: allowedLineIds);
        }

        return await ResolveFilterAsync(query, scope, allowedLineIds);
    }

    private bool TryResolveAccessScope(out BatchAccessScope scope, out IActionResult error)
    {
        scope = default!;
        error = default!;

        var customerValue = User.FindFirstValue("customerId");
        if (!int.TryParse(customerValue, out var customerId) || customerId <= 0)
        {
            error = Unauthorized(ApiResponse<object>.Fail("not_authenticated", "The access token does not contain a valid customerId claim."));
            return false;
        }

        var customerFactories = _factories.Query()
            .Where(x => x.CustomerId == customerId && x.IsActive && !x.IsDeleted)
            .Select(x => x.Id)
            .ToHashSet();
        var factoryClaims = GetPositiveIntClaims("factoryId");
        var factoryIds = factoryClaims.Count == 0 && User.IsInRole("Admin")
            ? customerFactories
            : customerFactories.Where(factoryClaims.Contains).ToHashSet();

        var customerLines = _lines.Query()
            .Where(x => factoryIds.Contains(x.FactoryId) && x.IsActive && !x.IsDeleted)
            .Select(x => new { x.Id, x.FactoryId })
            .ToList();
        var lineClaims = GetPositiveIntClaims("lineId");
        if (lineClaims.Count > 0)
        {
            customerLines = customerLines.Where(x => lineClaims.Contains(x.Id)).ToList();
        }

        scope = new BatchAccessScope(
            customerId,
            factoryIds,
            customerLines.ToDictionary(x => x.Id, x => x.FactoryId));
        return true;
    }

    private IActionResult? ValidateRequestedScope(
        BatchAccessScope scope,
        int? factoryId,
        int? lineId,
        out IReadOnlyCollection<int> allowedLineIds)
    {
        allowedLineIds = Array.Empty<int>();

        if (factoryId.HasValue && !scope.FactoryIds.Contains(factoryId.Value))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("factory_access_denied", "The selected factory is not available to the authenticated user."));
        }

        var lineFactoryId = 0;
        if (lineId.HasValue && !scope.LineFactoryIds.TryGetValue(lineId.Value, out lineFactoryId))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("line_access_denied", "The selected production line is not available to the authenticated user."));
        }

        if (factoryId.HasValue && lineId.HasValue && lineFactoryId != factoryId.Value)
        {
            return BadRequest(ApiResponse<object>.Fail("line_factory_mismatch", "The selected production line does not belong to the selected factory."));
        }

        allowedLineIds = lineId.HasValue
            ? new[] { lineId.Value }
            : scope.LineFactoryIds
                .Where(x => !factoryId.HasValue || x.Value == factoryId.Value)
                .Select(x => x.Key)
                .ToArray();
        return null;
    }

    private bool CanAccessBatch(int id, BatchAccessScope scope)
    {
        return Service.Query().Any(x =>
            x.Id == id &&
            x.CustomerId == scope.CustomerId &&
            scope.LineIds.Contains(x.LineId));
    }

    private FrontendFilter CreateAccessOnlyFilter(BatchAccessScope scope)
    {
        return new FrontendFilter(
            5,
            null,
            null,
            DateTime.MinValue,
            DateTime.MaxValue,
            null,
            null,
            "access_scope",
            "Batch access is restricted by the authenticated user token.",
            ApplyTimeFilter: false,
            CustomerId: scope.CustomerId,
            AllowedLineIds: scope.LineIds);
    }

    private HashSet<int> GetPositiveIntClaims(string claimType)
    {
        return User.FindAll(claimType)
            .Select(x => int.TryParse(x.Value, out var id) ? id : 0)
            .Where(x => x > 0)
            .ToHashSet();
    }

    private NotFoundObjectResult BatchNotFound(int id)
        => NotFound(ApiResponse<object>.Fail("not_found", $"Batch {id} was not found or is outside the authenticated user's access scope."));

    private static object[] CreateStatusOptions()
    {
        return new object[]
        {
            new { value = "planned", label = "Planned", canCreate = true, isFinal = false },
            new { value = "in_progress", label = "In Progress", canCreate = false, isFinal = false },
            new { value = "production", label = "Production", canCreate = false, isFinal = false },
            new { value = "finished", label = "Finished", canCreate = false, isFinal = true },
            new { value = "cancelled", label = "Cancelled", canCreate = false, isFinal = true },
            new { value = "split", label = "Split", canCreate = false, isFinal = true }
        };
    }

    private sealed record BatchAccessScope(
        int CustomerId,
        IReadOnlySet<int> FactoryIds,
        IReadOnlyDictionary<int, int> LineFactoryIds)
    {
        public IReadOnlyCollection<int> LineIds => LineFactoryIds.Keys.ToArray();
    }

    private static int SafePage(int page) => page <= 0 ? 1 : page;
    private static int SafePageSize(int pageSize) => pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
}
