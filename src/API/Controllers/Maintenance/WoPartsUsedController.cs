using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Maintenance;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Maintenance;

[Route("v1/wo-parts-used")]
public class WoPartsUsedController : CrudController<WoPartUsed, IWoPartUsedService>
{
    public WoPartsUsedController(IWoPartUsedService service) : base(service)
    {
    }

}
