using API.Common;
using API.Contracts;
using Application.Abstractions.Services;
using Application.Common.Time;
using Application.Dtos.Frontend;
using Domain.Entities.AssetRegistry;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Assets;

[Route("v1/assets")]
public class AssetsController : CrudController<Asset, IAssetService>
{
    private readonly IFrontendDataService _frontend;
    private readonly ITimeUtilities _timeUtilities;

    public AssetsController(IAssetService service, IFrontendDataService frontend, ITimeUtilities timeUtilities) : base(service)
    {
        _frontend = frontend;
        _timeUtilities = timeUtilities;
    }

    [HttpGet]
    public override async Task<IActionResult> GetAll()
    {
        var filter = await ResolveFilterAsync(new FrontendFilterQuery());
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetAssetsTableAsync(filter), filter));
    }

    [HttpGet("{id:int}/history")]
    public IActionResult History(int id)
    {
        return StaticOk($"/v1/assets/{id}/history", new { id }, "Type D: future asset history read model. Bind asset table now, then replace this with AssetHistoryQueryService.");
    }

    [HttpGet("health-summary")]
    public async Task<IActionResult> HealthSummary([FromQuery] FrontendFilterQuery query)
    {
        var filter = await ResolveFilterAsync(query);
        return Ok(ApiResponse<object?>.Ok(await _frontend.GetAssetsKpisAsync(filter), filter));
    }

    private async Task<FrontendFilter> ResolveFilterAsync(FrontendFilterQuery query)
        => FrontendFilter.FromResolved(await _timeUtilities.ResolveAsync(query.ToInput()));
}
