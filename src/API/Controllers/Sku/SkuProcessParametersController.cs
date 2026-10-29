using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.MasterData;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Sku;

[Route("v1/sku-process-parameters")]
public class SkuProcessParametersController : CrudController<SkuProcessParameter, ISkuProcessParameterService>
{
    public SkuProcessParametersController(ISkuProcessParameterService service) : base(service)
    {
    }

}
