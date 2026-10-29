using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers.Org;

[Route("v1/factories")]
public class FactoriesController : CrudController<Factory, IFactoryService>
{
    private readonly IProductionLineService _lineService;
    private readonly IMachineService _machineService;

    public FactoriesController(IFactoryService service, IProductionLineService lineService, IMachineService machineService) : base(service)
    {
        _lineService = lineService;
        _machineService = machineService;
    }

    [Authorize]
    [HttpGet("by-customer")]
    public IActionResult GetByCustomer()
    {
        var customerValue = User.FindFirstValue("customerId");
        if (!int.TryParse(customerValue, out var customerId) || customerId <= 0)
        {
            return Unauthorized(ApiResponse<object>.Fail(
                "not_authenticated",
                "The access token does not contain a valid customerId claim."));
        }

        var factoryIds = User.FindAll("factoryId")
            .Select(x => int.TryParse(x.Value, out var id) ? id : 0)
            .Where(x => x > 0)
            .ToHashSet();

        var query = Service.Query()
            .Where(x => x.CustomerId == customerId && x.IsActive && !x.IsDeleted);

        if (factoryIds.Count > 0)
        {
            query = query.Where(x => factoryIds.Contains(x.Id));
        }
        else if (!User.IsInRole("Admin"))
        {
            query = query.Where(_ => false);
        }

        var factories = query
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.CustomerId, x.Code, x.Name, x.City, x.CountryCode, x.TimeZone, x.IsActive })
            .ToList<object>();

        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(factories));
    }

    [HttpGet("{id:int}/lines")]
    public IActionResult GetLines(int id)
    {
        var lines = _lineService.Query()
            .Where(x => x.FactoryId == id && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.FactoryId, x.Code, x.Name, x.ProductType, x.MaxSpeedUpm, x.IsActive })
            .ToList<object>();
        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(lines));
    }

    [HttpGet("{id:int}/summary")]
    public IActionResult GetSummary(int id)
    {
        var lines = _lineService.Query().Where(x => x.FactoryId == id).ToList();
        var lineIds = lines.Select(x => x.Id).ToHashSet();
        var machines = _machineService.Query().Where(x => lineIds.Contains(x.LineId)).ToList();
        return Ok(ApiResponse<object>.Ok(new
        {
            factoryId = id,
            lines = lines.Count,
            machines = machines.Count,
            activeMachines = machines.Count(x => x.IsActive),
            futureNote = "Type D: replace with VW_FactorySummary when dashboards need faster aggregation."
        }));
    }
}
