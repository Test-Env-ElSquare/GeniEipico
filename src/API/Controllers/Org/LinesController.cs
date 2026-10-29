using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Organization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Org;

[Route("v1/lines")]
public class LinesController : CrudController<ProductionLine, IProductionLineService>
{
    public LinesController(IProductionLineService service) : base(service)
    {
    }

    [HttpGet("all")]
    public IActionResult GetAllLines()
    {
        var lines = Service.Query()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.FactoryId)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.FactoryId,
                x.Code,
                x.Name,
                x.ProductType,
                x.MaxSpeedUpm,
                x.IsActive
            })
            .ToList<object>();

        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(lines));
    }

    [HttpGet("by-factory/{factoryId:int}")]
    public IActionResult GetByFactory(int factoryId)
    {
        var lines = Service.Query()
            .Where(x => x.FactoryId == factoryId && x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.FactoryId, x.Code, x.Name, x.ProductType, x.MaxSpeedUpm, x.IsActive })
            .ToList<object>();

        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(lines));
    }
}
