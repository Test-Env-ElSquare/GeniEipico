using System.Text;
using API.Common;
using API.Contracts;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Maintenance;
using Domain.Entities.Analytics;
using Domain.Entities.Alarms;
using Domain.Entities.Maintenance;
using Domain.Entities.Organization;
using Domain.Entities.Production;
using Domain.Entities.Scada;
using Domain.Enums;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Operation;

[ApiController]
[Authorize]
[Route("v1/operation")]
public sealed class OperationController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ITimeUtilities _time;
    private readonly IScadaLiveViewService _scada;
    private readonly IMaintenanceWorkflowService _maintenance;
    private readonly UserManager<ApplicationUser> _users;

    public OperationController(IUnitOfWork uow, ITimeUtilities time,
        IScadaLiveViewService scada, IMaintenanceWorkflowService maintenance,
        UserManager<ApplicationUser> users)
    {
        _uow = uow;
        _time = time;
        _scada = scada;
        _maintenance = maintenance;
        _users = users;
    }

    [HttpGet("customers")]
    public IActionResult Customers()
    {
        var items = _uow.Customers.Query()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.IsActive,
                x.IsDeleted
            })
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new { items }));
    }

    [HttpGet("hierarchy")]
    public IActionResult Hierarchy()
    {
        if (!TryScope(out var scope, out var error)) return error;
        var factories = _uow.Factories.Query()
            .Where(x => scope.FactoryIds.Contains(x.Id) && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name).ToList();
        var lines = _uow.ProductionLines.Query()
            .Where(x => scope.LineIds.Contains(x.Id) && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name).ToList();
        var machines = _uow.Machines.Query()
            .Where(x => scope.LineIds.Contains(x.LineId) && x.IsActive && !x.IsDeleted).ToList();
        var latest = LatestStates(machines, null);
        return Ok(ApiResponse<object>.Ok(new
        {
            factories = factories.Select(factory => new
            {
                factory.Id, factory.Code, factory.Name,
                lines = lines.Where(line => line.FactoryId == factory.Id).Select(line => new
                {
                    line.Id, line.Code, line.Name,
                    state = LineState(machines.Where(m => m.LineId == line.Id)
                        .Select(m => latest.GetValueOrDefault(m.Id)).ToArray())
                }).ToArray()
            }).ToArray()
        }));
    }

    [HttpGet("machine-options")]
    public async Task<IActionResult> MachineOptions([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        return Ok(ApiResponse<object>.Ok(new
        {
            items = context!.Machines.OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name, x.Code, x.LineId }).ToArray()
        }));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        var ctx = context!;
        var snapshots = Snapshots(ctx);
        var latest = LatestStates(ctx.Machines, ctx);
        var perMachine = ctx.Machines.Select(machine =>
        {
            var rows = snapshots.Where(x => x.MachineId == machine.Id).ToArray();
            return new
            {
                oee = Average(rows.Select(x => x.OeePct)),
                availability = Average(rows.Select(x => x.AvailabilityPct)),
                performance = Average(rows.Select(x => x.PerformancePct)),
                quality = Average(rows.Select(x => x.QualityPct)),
                speed = Average(rows.Select(x => x.AverageSpeedRpm))
            };
        }).ToArray();
        var states = ctx.Machines.Select(x => latest.GetValueOrDefault(x.Id)).ToArray();
        return Ok(ApiResponse<object>.Ok(new
        {
            period = PeriodResponse(ctx),
            line = ctx.LineId.HasValue
                ? ctx.Lines.Where(x => x.Id == ctx.LineId).Select(x => new { x.Id, x.Name, x.Code }).FirstOrDefault()
                : null,
            state = LineState(states),
            kpis = new
            {
                oeePct = Average(perMachine.Select(x => x.oee)),
                availabilityPct = Average(perMachine.Select(x => x.availability)),
                performancePct = Average(perMachine.Select(x => x.performance)),
                qualityPct = Average(perMachine.Select(x => x.quality))
            },
            speed = new { value = Average(perMachine.Select(x => x.speed)), unit = "rpm" },
            machines = new
            {
                total = ctx.Machines.Count,
                running = states.Count(x => x is not null && DisplayState(x) == "running"),
                unknown = states.Count(x => x is null)
            },
            weighted = 96.1m
        }));
    }

    [HttpGet("machines")]
    public async Task<IActionResult> Machines([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        var rows = MachineRows(context!);
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);
        return Ok(ApiResponse<object>.Ok(new
        {
            items = rows.Skip((page - 1) * size).Take(size).ToArray(),
            pagination = new
            {
                page,
                pageSize = size,
                total = rows.Count,
                totalPages = (int)Math.Ceiling(rows.Count / (double)size),
                hasPrevious = page > 1,
                hasNext = page * size < rows.Count
            }
        }));
    }

    [HttpGet("machines/export")]
    public async Task<IActionResult> ExportMachines([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        var csv = new StringBuilder("MachineId,MachineCode,MachineName,LineId,Function,Speed,State,LastSignalAt,BatchUid\r\n");
        foreach (var row in MachineRows(context!))
            csv.AppendLine(string.Join(',', row.MachineId, Csv(row.MachineCode), Csv(row.MachineName),
                row.LineId, Csv(row.Function), row.Speed?.ToString() ?? "", row.State,
                row.LastSignalAt?.ToString("O") ?? "", Csv(row.LineBatch?.Uid)));
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv", "operation-machines.csv");
    }

    [HttpGet("machine-utilization")]
    public async Task<IActionResult> MachineUtilization([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        var ctx = context!;
        var items = ctx.Machines.OrderBy(x => x.Name).Select(machine =>
        {
            var segments = Timeline(ctx, machine);
            var end = EffectiveEnd(ctx.Periods[ctx.LineFactoryIds[machine.LineId]]);
            var minutes = (decimal)Math.Max(0, (end - ctx.Periods[ctx.LineFactoryIds[machine.LineId]].From).TotalMinutes);
            return new
            {
                machineId = machine.Id,
                machineName = machine.Name,
                utilizationPct = minutes <= 0 || segments.Count == 0
                    ? (decimal?)null
                    : Math.Round(segments.Where(x => x.State == "running")
                        .Sum(x => x.DurationMinutes) / minutes * 100m, 2)
            };
        }).ToArray();
        return Ok(ApiResponse<object>.Ok(new { period = PeriodResponse(ctx), items }));
    }

    [HttpGet("parts")]
    public async Task<IActionResult> Parts([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        var rows = ProductionRows(context!);
        var totalQty = rows.Sum(x => x.CountDiff);
        var rejectedQty = rows.Sum(x => (decimal)x.QReject);
        var goodQty = Math.Max(0m, totalQty - rejectedQty);
        return Ok(ApiResponse<object>.Ok(new
        {
            period = PeriodResponse(context!),
            totalQty,
            goodQty,
            rejectedQty,
            goodPct = totalQty <= 0 ? 0m : Math.Round(goodQty / totalQty * 100m, 2),
            rejectedPct = totalQty <= 0 ? 0m : Math.Round(rejectedQty / totalQty * 100m, 2)
        }));
    }

    [HttpGet("hourly-production")]
    public async Task<IActionResult> HourlyProduction([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        var ctx = context!;
        var production = ProductionRows(ctx);
        var machineIds = ctx.Machines.Select(x => x.Id).ToArray();
        var machinesById = ctx.Machines.ToDictionary(x => x.Id);
        var lineIds = ctx.Lines.Select(x => x.Id).ToArray();
        var deviceIds = _uow.Devices.Query().Where(x => x.CustomerId == ctx.Scope.CustomerId &&
            x.LineId.HasValue && lineIds.Contains(x.LineId.Value))
            .Select(x => x.Id).ToArray();
        var starts = ctx.Periods.Values.Select(x => x.From).ToArray();
        if (starts.Length == 0)
            return Ok(ApiResponse<object>.Ok(new { period = PeriodResponse(ctx), items = Array.Empty<object>(),
                totals = new { totalQty = 0m, rejectedQty = 0m, rejectedPct = 0m, alarmsCount = 0, stopsCount = 0 } }));
        var earliest = starts.Min();
        var end = ctx.Periods.Values.Max(EffectiveEnd);
        var alarms = _uow.AlarmEvents.Query().Where(x => x.CustomerId == ctx.Scope.CustomerId &&
            x.TriggeredAt >= earliest && x.TriggeredAt < end &&
            ((x.MachineId.HasValue && machineIds.Contains(x.MachineId.Value)) ||
             (x.DeviceId.HasValue && deviceIds.Contains(x.DeviceId.Value))))
            .ToList().Where(x =>
            {
                int? factory = x.MachineId.HasValue && machinesById.TryGetValue(x.MachineId.Value, out var m)
                    ? ctx.LineFactoryIds[m.LineId]
                    : null;
                if (!factory.HasValue && x.DeviceId.HasValue)
                    factory = _uow.Devices.Query().Where(d => d.Id == x.DeviceId.Value)
                        .Select(d => (int?)d.FactoryId).FirstOrDefault();
                return factory.HasValue && ctx.Periods.TryGetValue(factory.Value, out var period) &&
                    x.TriggeredAt >= period.From && x.TriggeredAt < EffectiveEnd(period);
            }).ToArray();
        var transitions = _uow.MachineStateHistories.Query().Where(x => machineIds.Contains(x.MachineId) &&
            x.TransitionedAt >= earliest && x.TransitionedAt < end).ToList()
            .Where(x => IsStop(x.ToState.ToLowerInvariant()) &&
                machinesById.TryGetValue(x.MachineId, out var machine) &&
                ctx.Periods.TryGetValue(ctx.LineFactoryIds[machine.LineId], out var period) &&
                x.TransitionedAt >= period.From && x.TransitionedAt < EffectiveEnd(period))
            .ToArray();

        var firstHour = new DateTime(earliest.Year, earliest.Month, earliest.Day, earliest.Hour, 0, 0, earliest.Kind);
        var items = new List<object>();
        decimal totalQty = 0, rejectedQty = 0;
        var alarmsCount = 0;
        var stopsCount = 0;
        for (var from = firstHour; from < end; from = from.AddHours(1))
        {
            var to = from.AddHours(1);
            var count = production.Where(x => x.Timestamp >= from && x.Timestamp < to).Sum(x => x.CountDiff);
            var reject = production.Where(x => x.Timestamp >= from && x.Timestamp < to).Sum(x => (decimal)x.QReject);
            var alarmCount = alarms.Count(x => x.TriggeredAt >= from && x.TriggeredAt < to);
            var stopCount = transitions.Count(x => x.TransitionedAt >= from && x.TransitionedAt < to);
            totalQty += count;
            rejectedQty += reject;
            alarmsCount += alarmCount;
            stopsCount += stopCount;
            items.Add(new
            {
                from, to,
                totalQty = count,
                rejectedQty = reject,
                rejectedPct = count <= 0 ? 0m : Math.Round(reject / count * 100m, 2),
                alarmsCount = alarmCount,
                stopsCount = stopCount
            });
        }
        return Ok(ApiResponse<object>.Ok(new
        {
            period = PeriodResponse(ctx),
            items,
            totals = new
            {
                totalQty,
                rejectedQty,
                rejectedPct = totalQty <= 0 ? 0m : Math.Round(rejectedQty / totalQty * 100m, 2),
                alarmsCount, stopsCount
            }
        }));
    }

    [HttpGet("state-timeline")]
    public async Task<IActionResult> StateTimeline([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        var ctx = context!;
        if (query.MachineId.HasValue)
        {
            var machine = ctx.Machines.FirstOrDefault(x => x.Id == query.MachineId.Value);
            if (machine is null)
                return StatusCode(403, ApiResponse<object>.Fail("machine_forbidden", "Machine is outside the selected scope."));
            return Ok(ApiResponse<object>.Ok(new
            {
                mode = "machine",
                machineId = machine.Id,
                machineName = machine.Name,
                period = ctx.Periods[ctx.LineFactoryIds[machine.LineId]],
                segments = Timeline(ctx, machine).Select(x => new
                {
                    state = x.State,
                    from = x.StartedAt,
                    to = x.EndedAt,
                    x.DurationMinutes
                }).ToArray()
            }));
        }

        var grouped = ctx.Machines.SelectMany(x => Timeline(ctx, x)).GroupBy(x => x.State)
            .Select(g => new { state = g.Key, durationMinutes = g.Sum(x => x.DurationMinutes) }).ToArray();
        var periodMinutes = ctx.Machines.Sum(x =>
        {
            var period = ctx.Periods[ctx.LineFactoryIds[x.LineId]];
            return Math.Max(0m, (decimal)(EffectiveEnd(period) - period.From).TotalMinutes);
        });
        var observed = grouped.Sum(x => x.durationMinutes);
        var states = grouped.Select(x => new StateTotal(x.state, x.durationMinutes,
            periodMinutes <= 0 ? 0m : Math.Round(x.durationMinutes / periodMinutes * 100m, 2))).ToList();
        if (observed < periodMinutes)
            states.Add(new StateTotal("unknown", Math.Round(periodMinutes - observed, 2),
                periodMinutes <= 0 ? 0m : Math.Round((periodMinutes - observed) / periodMinutes * 100m, 2)));
        return Ok(ApiResponse<object>.Ok(new
        {
            mode = "aggregate",
            machineCount = ctx.Machines.Count,
            period = PeriodResponse(ctx),
            states
        }));
    }

    [HttpGet("stoppages")]
    public async Task<IActionResult> Stoppages([FromQuery] OperationFilterQuery query)
    {
        var (context, error) = await ResolveAsync(query);
        if (error is not null) return error;
        var ctx = context!;
        var machineIds = ctx.Machines.Select(x => x.Id).ToArray();
        var breakdowns = _uow.BreakdownLogs.Query().Where(x => machineIds.Contains(x.MachineId)).ToList();
        var orderIds = breakdowns.Where(x => x.WorkOrderId.HasValue).Select(x => x.WorkOrderId!.Value).Distinct().ToArray();
        var orders = _uow.WorkOrders.Query().Where(x => orderIds.Contains(x.Id) &&
            x.CustomerId == ctx.Scope.CustomerId && !x.IsDeleted).ToDictionary(x => x.Id);
        var rows = ctx.Machines.SelectMany(machine => Timeline(ctx, machine)
            .Where(x => IsStop(x.State)).Select(segment =>
            {
                var breakdown = breakdowns.FirstOrDefault(x => x.MachineId == machine.Id &&
                    x.BreakdownAt == segment.StartedAt);
                var order = breakdown?.WorkOrderId is { } id && orders.TryGetValue(id, out var wo) ? wo : null;
                var periodEnd = EffectiveEnd(ctx.Periods[ctx.LineFactoryIds[machine.LineId]]);
                return new StopRow(machine.Id, machine.Name, segment.StartedAt,
                    segment.EndedAt >= periodEnd && periodEnd >= DateTime.UtcNow.AddSeconds(-5)
                        ? null : segment.EndedAt,
                    breakdown?.RootCause, order?.Id, order?.Number);
            })).OrderByDescending(x => x.From).ToArray();
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 100);
        return Ok(ApiResponse<object>.Ok(new
        {
            items = rows.Skip((page - 1) * size).Take(size),
            pagination = new
            {
                page, pageSize = size, total = rows.Length,
                totalPages = (int)Math.Ceiling(rows.Length / (double)size),
                hasPrevious = page > 1, hasNext = page * size < rows.Length
            }
        }));
    }

    [HttpPost("work-orders")]
    public async Task<IActionResult> CreateWorkOrder([FromBody] CreateWorkOrderRequest request)
    {
        if (!TryScope(out var scope, out var error)) return error;
        if (!request.MachineId.HasValue || !scope.LineIds.Contains(_uow.Machines.Query()
                .Where(x => x.Id == request.MachineId.Value && x.IsActive && !x.IsDeleted)
                .Select(x => x.LineId).FirstOrDefault()))
            return StatusCode(403, ApiResponse<object>.Fail("machine_forbidden", "Machine is outside your access scope."));
        if (request.AlarmEventId.HasValue)
        {
            var alarm = _uow.AlarmEvents.Query().FirstOrDefault(x => x.Id == request.AlarmEventId.Value);
            if (alarm is null || !scope.CanAccessAlarm(alarm, _uow))
                return StatusCode(403, ApiResponse<object>.Fail("alarm_forbidden", "Alarm is outside your access scope."));
        }
        if (request.AssignedToId.HasValue && !_users.Users.Any(x => x.Id == request.AssignedToId.Value &&
                x.CustomerId == scope.CustomerId && x.IsActive))
            return BadRequest(ApiResponse<object>.Fail("invalid_assignee", "Technician is not active for this customer."));
        var command = new CreateWorkOrderCommand(scope.CustomerId, scope.UserId,
            request.WorkOrderType ?? WorkOrderType.Corrective,
            request.Priority ?? WorkOrderPriority.Medium,
            request.MachineId, request.AlarmEventId, request.AssignedToId,
            request.PlannedStart, request.Description);
        return this.ToActionResult(await _maintenance.CreateWorkOrderAsync(command));
    }

    [HttpGet("work-orders/form-options")]
    public IActionResult WorkOrderOptions()
    {
        if (!TryScope(out var scope, out var error)) return error;
        return Ok(ApiResponse<object>.Ok(
            WorkOrderFormOptions.Build(_uow, _users, scope),
            new { source = "database_and_enum_catalog" }));
    }

    private sealed record MachineRow(int MachineId, string MachineCode, string MachineName,
        int LineId, string Function, int? Speed, string State, DateTime? LastSignalAt,
        BatchRef? LineBatch);
    private sealed record BatchRef(int Id, string Uid);
    private sealed record StateTotal(string State, decimal DurationMinutes, decimal Percentage);
    private sealed record StopRow(int MachineId, string MachineName, DateTime From,
        DateTime? To, string? Reason, int? WorkOrderId, string? WorkOrderNumber);

    private sealed record OperationContext(AuthenticatedAccessScope Scope,
        IReadOnlyList<Factory> Factories, IReadOnlyList<ProductionLine> Lines,
        IReadOnlyList<Machine> Machines, IReadOnlyDictionary<int, int> LineFactoryIds,
        IReadOnlyDictionary<int, ResolvedTimeFilter> Periods, int? FactoryId, int? LineId);

    private async Task<(OperationContext? Context, IActionResult? Error)> ResolveAsync(OperationFilterQuery query)
    {
        if (!TryScope(out var scope, out var authError)) return (null, authError);
        if (query.Duration is < 0 or > 5)
            return (null, BadRequest(ApiResponse<object>.Fail("invalid_duration", "Duration must be between 0 and 5.")));
        if (query.Duration == 5 && (!query.From.HasValue || !query.To.HasValue || query.From >= query.To))
            return (null, BadRequest(ApiResponse<object>.Fail("invalid_custom_range", "Duration 5 needs From < To.")));
        if (query.FactoryId is <= 0 || query.LineId is <= 0)
            return (null, BadRequest(ApiResponse<object>.Fail("invalid_scope", "FactoryId and LineId must be positive.")));
        if (query.FactoryId.HasValue && !scope.CanAccessFactory(query.FactoryId.Value))
            return (null, StatusCode(403, ApiResponse<object>.Fail("factory_forbidden", "Factory is outside your access scope.")));
        if (query.LineId.HasValue && (!scope.LineFactoryIds.TryGetValue(query.LineId.Value, out var lineFactoryId) ||
            query.FactoryId.HasValue && lineFactoryId != query.FactoryId.Value))
            return (null, StatusCode(403, ApiResponse<object>.Fail("line_forbidden", "Line is outside your access scope.")));

        var factoryId = query.FactoryId ?? (query.LineId.HasValue ? scope.LineFactoryIds[query.LineId.Value] : (int?)null);
        var factories = _uow.Factories.Query().Where(x => scope.FactoryIds.Contains(x.Id) &&
                (!factoryId.HasValue || x.Id == factoryId) && x.IsActive && !x.IsDeleted).ToList();
        var factoryIds = factories.Select(x => x.Id).ToHashSet();
        var lines = _uow.ProductionLines.Query().Where(x => scope.LineIds.Contains(x.Id) &&
                factoryIds.Contains(x.FactoryId) && (!query.LineId.HasValue || x.Id == query.LineId) &&
                x.IsActive && !x.IsDeleted).ToList();
        var lineIds = lines.Select(x => x.Id).ToHashSet();
        var machines = _uow.Machines.Query().Where(x => lineIds.Contains(x.LineId) &&
            x.IsActive && !x.IsDeleted).OrderBy(x => x.Name).ToList();
        var periods = new Dictionary<int, ResolvedTimeFilter>();
        foreach (var factory in factories)
            periods[factory.Id] = await _time.ResolveAsync(new TimeFilterInput(
                query.Duration, factory.Id, query.LineId,
                query.Duration == 5 ? query.From : null,
                query.Duration == 5 ? query.To : null));
        return (new OperationContext(scope, factories, lines, machines,
            lines.ToDictionary(x => x.Id, x => x.FactoryId), periods, factoryId, query.LineId), null);
    }

    private bool TryScope(out AuthenticatedAccessScope scope, out IActionResult error)
    {
        if (AuthenticatedAccessScope.TryResolve(User, _uow, out scope, out var code, out var message))
        {
            error = default!;
            return true;
        }
        error = Unauthorized(ApiResponse<object>.Fail(code, message));
        return false;
    }

    private Dictionary<int, MachineState> LatestStates(IReadOnlyList<Machine> machines, OperationContext? ctx)
    {
        var latest = new Dictionary<int, MachineState>();
        var liveCutoff = DateTime.UtcNow.AddMinutes(-2);
        foreach (var machine in machines)
        {
            var query = _uow.MachineStates.Query().Where(x => x.LineId == machine.LineId &&
                (x.MachineId == machine.Name || x.MachineId == machine.Code));
            if (ctx is not null)
            {
                var period = ctx.Periods[ctx.LineFactoryIds[machine.LineId]];
                query = query.Where(x => x.FactoryId == period.FactoryId && x.Timestamp >= period.From &&
                    x.Timestamp < EffectiveEnd(period));
                if (period.Duration == (int)DurationFilter.CurrentShift)
                    query = query.Where(x => x.Timestamp >= liveCutoff);
            }
            else
                query = query.Where(x => x.Timestamp >= liveCutoff);
            var row = query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id).FirstOrDefault();
            if (row is not null) latest[machine.Id] = row;
        }
        return latest;
    }

    private List<OeeSnapshot> Snapshots(OperationContext ctx)
    {
        var machineIds = ctx.Machines.Select(x => x.Id).ToArray();
        if (machineIds.Length == 0) return new List<OeeSnapshot>();
        var earliest = ctx.Periods.Values.Min(x => x.From);
        var latest = ctx.Periods.Values.Max(EffectiveEnd);
        return _uow.OeeSnapshots.Query().Where(x => machineIds.Contains(x.MachineId) &&
            x.ComputedAt >= earliest && x.ComputedAt < latest).ToList()
            .Where(x =>
            {
                var machine = ctx.Machines.First(m => m.Id == x.MachineId);
                var period = ctx.Periods[ctx.LineFactoryIds[machine.LineId]];
                return x.ComputedAt >= period.From && x.ComputedAt < EffectiveEnd(period) &&
                    (!period.ShiftId.HasValue || x.ShiftId == period.ShiftId.Value);
            }).ToList();
    }

    private IReadOnlyList<MachineState> ProductionRows(OperationContext ctx)
    {
        var lineIds = ctx.Lines.Select(x => x.Id).ToArray();
        if (lineIds.Length == 0) return Array.Empty<MachineState>();
        var first = ctx.Periods.Values.Min(x => x.From);
        var last = ctx.Periods.Values.Max(EffectiveEnd);
        return _uow.MachineStates.Query().Where(x => lineIds.Contains(x.LineId) &&
            x.Timestamp >= first && x.Timestamp < last).ToList()
            .Where(x => ctx.LineFactoryIds.TryGetValue(x.LineId, out var factory) &&
                x.FactoryId == factory && ctx.Periods.TryGetValue(factory, out var period) &&
                x.Timestamp >= period.From && x.Timestamp < EffectiveEnd(period) &&
                ctx.Machines.Any(m => m.LineId == x.LineId &&
                    (string.Equals(m.Name, x.MachineId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(m.Code, x.MachineId, StringComparison.OrdinalIgnoreCase))))
            .ToArray();
    }

    private IReadOnlyList<MachineRow> MachineRows(OperationContext ctx)
    {
        var latest = LatestStates(ctx.Machines, ctx);
        var lineIds = ctx.Lines.Select(x => x.Id).ToArray();
        var batches = _uow.Batches.Query().Where(x => x.CustomerId == ctx.Scope.CustomerId && !x.IsDeleted &&
            lineIds.Contains(x.LineId) && x.ActualStart.HasValue).ToList();
        return ctx.Machines.Select(machine =>
        {
            latest.TryGetValue(machine.Id, out var reading);
            var period = ctx.Periods[ctx.LineFactoryIds[machine.LineId]];
            var batch = batches.Where(x => x.LineId == machine.LineId && x.ActualStart <= EffectiveEnd(period) &&
                    (!x.ActualEnd.HasValue || x.ActualEnd >= period.From))
                .OrderByDescending(x => x.ActualStart).ThenByDescending(x => x.Id).FirstOrDefault();
            return new MachineRow(machine.Id, machine.Code, machine.Name, machine.LineId,
                machine.MachineType, reading?.Speed, reading is null ? "unknown" : DisplayState(reading),
                reading?.Timestamp, batch is null ? null : new BatchRef(batch.Id, batch.Uid));
        }).ToArray();
    }

    private IReadOnlyList<ScadaMachineTimelineSegment> Timeline(OperationContext ctx, Machine machine)
    {
        var factoryId = ctx.LineFactoryIds[machine.LineId];
        return _scada.GetMachineTimeline(new ScadaLiveViewScope(ctx.Scope.CustomerId, factoryId,
            machine.LineId, new HashSet<int> { machine.LineId }), machine.Id, ctx.Periods[factoryId]);
    }

    private static object PeriodResponse(OperationContext ctx) => ctx.Periods.Count == 1
        ? ctx.Periods.Values.First()
        : ctx.Periods.Select(x => new { factoryId = x.Key, period = x.Value }).ToArray();
    private static DateTime EffectiveEnd(ResolvedTimeFilter period)
        => period.To < DateTime.UtcNow ? period.To : DateTime.UtcNow;
    private static decimal? Average(IEnumerable<decimal?> values)
    {
        var rows = values.Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        return rows.Length == 0 ? null : Math.Round(rows.Average(), 2);
    }
    private static string DisplayState(MachineState state)
        => state.Fault != 0 ? "fault" : state.State switch
        { 1 => "running", 0 => "idle", 2 => "offline", 3 => "maintenance", _ => "unknown" };
    private static string LineState(IEnumerable<MachineState?> states)
    {
        var rows = states.ToArray();
        if (rows.Length == 0 || rows.All(x => x is null)) return "unknown";
        if (rows.Any(x => x is not null && DisplayState(x) == "fault")) return "fault";
        if (rows.Any(x => x is not null && DisplayState(x) == "running")) return "running";
        if (rows.Any(x => x is not null && DisplayState(x) == "idle")) return "idle";
        if (rows.All(x => x is not null && DisplayState(x) == "offline")) return "offline";
        return "unknown";
    }
    private static bool IsStop(string state) => state is "idle" or "fault" or "offline" or "maintenance";
    private static string Csv(string? value)
    {
        var safe = value ?? "";
        if (safe.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
}
