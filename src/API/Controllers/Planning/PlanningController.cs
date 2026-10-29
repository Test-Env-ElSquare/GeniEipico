using API.Common;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Common.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace API.Controllers.Planning;

public sealed class PlanningFilterQuery
{
    [Range(0, 5)] public int Duration { get; set; }
    [Range(1, int.MaxValue)] public int? FactoryId { get; set; }
    [Range(1, int.MaxValue)] public int? LineId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 10;
}

[ApiController]
[Authorize]
[Route("v1/planning")]
public sealed class PlanningController(IUnitOfWork uow, ITimeUtilities time, IPlanningService planning) : ControllerBase
{
    [HttpGet("summary")]
    public Task<IActionResult> Summary([FromQuery] PlanningFilterQuery query) => Read("summary", query);
    [HttpGet("schedule")]
    public Task<IActionResult> Schedule([FromQuery] PlanningFilterQuery query) => Read("schedule", query);
    [HttpGet("planned-vs-produced")]
    public Task<IActionResult> PlannedVsProduced([FromQuery] PlanningFilterQuery query) => Read("planned-vs-produced", query);
    [HttpGet("batches")]
    public Task<IActionResult> Batches([FromQuery] PlanningFilterQuery query) => Read("batches", query);
    [HttpGet("routes")]
    public Task<IActionResult> Routes([FromQuery] PlanningFilterQuery query) => Read("routes", query);
    [HttpGet("mrp")]
    public Task<IActionResult> Mrp([FromQuery] PlanningFilterQuery query) => Read("mrp", query);
    [HttpGet("stoppages")]
    public Task<IActionResult> Stoppages([FromQuery] PlanningFilterQuery query) => Read("stoppages", query);
    [HttpGet("form-options")]
    public async Task<IActionResult> FormOptions([FromQuery] PlanningFilterQuery query)
    {
        var (scope, error) = await Resolve(query);
        return error ?? Ok(ApiResponse<object>.Ok(await planning.FormOptionsAsync(scope!)));
    }
    [HttpPost("schedules")]
    public async Task<IActionResult> AddSchedule(ScheduleBatchCommand command)
    {
        var (scope, error) = await Resolve(new());
        return error ?? Result(await planning.ScheduleAsync(scope!, command));
    }
    [HttpPost("stoppages")]
    public async Task<IActionResult> AddStoppage(PlanningStoppageCommand command)
    {
        var (scope, error) = await Resolve(new());
        return error ?? Result(await planning.StoppageAsync(scope!, command));
    }
    [HttpPost("routes")]
    public async Task<IActionResult> AddRoute(PlanningRouteCommand command)
    {
        var (scope, error) = await Resolve(new());
        return error ?? Result(await planning.RouteAsync(scope!, command));
    }
    private IActionResult Result(WorkflowResult<object> result)
        => result.Succeeded ? Ok(ApiResponse<object>.Ok(result.Data))
            : StatusCode(result.ErrorCode == "forbidden" ? 403 : result.ErrorCode is "overlap" or "already_scheduled" ? 409 : 400,
                ApiResponse<object>.Fail(result.ErrorCode!, result.Message!));
    private async Task<IActionResult> Read(string section, PlanningFilterQuery query)
    {
        var (scope, error) = await Resolve(query);
        return error ?? Ok(ApiResponse<object>.Ok(await planning.ReadAsync(section, scope!, query.Page, query.PageSize)));
    }
    private async Task<(PlanningScope? Scope, IActionResult? Error)> Resolve(PlanningFilterQuery query)
    {
        if (!AuthenticatedAccessScope.TryResolve(User, uow, out var access, out var code, out var message))
            return (null, Unauthorized(ApiResponse<object>.Fail(code, message)));
        if ((query.Duration == 5 || query.From.HasValue || query.To.HasValue) &&
            (!query.From.HasValue || !query.To.HasValue || query.From >= query.To))
            return (null, BadRequest(ApiResponse<object>.Fail("invalid_period", "Supply both From and To, with From < To.")));
        if (query.From.HasValue && query.To - query.From > TimeSpan.FromDays(366))
            return (null, BadRequest(ApiResponse<object>.Fail("invalid_period", "Maximum custom window is 366 days.")));
        if (query.FactoryId.HasValue && !access.FactoryIds.Contains(query.FactoryId.Value))
            return (null, StatusCode(403, ApiResponse<object>.Fail("forbidden", "Factory is outside your scope.")));
        if (query.LineId.HasValue && (!access.LineFactoryIds.TryGetValue(query.LineId.Value, out var factory) ||
            query.FactoryId.HasValue && query.FactoryId != factory))
            return (null, StatusCode(403, ApiResponse<object>.Fail("forbidden", "Line is outside your scope.")));
        var lines = access.LineFactoryIds.Where(x => (!query.FactoryId.HasValue || x.Value == query.FactoryId) &&
            (!query.LineId.HasValue || x.Key == query.LineId)).ToDictionary(x => x.Key, x => x.Value);
        var periods = new Dictionary<int, ResolvedTimeFilter>();
        foreach (var factoryId in lines.Values.Distinct())
            periods[factoryId] = await time.ResolveAsync(new(query.Duration, factoryId, query.LineId,
                query.From?.UtcDateTime, query.To?.UtcDateTime));
        return (new(access.CustomerId, lines, periods), null);
    }
}
