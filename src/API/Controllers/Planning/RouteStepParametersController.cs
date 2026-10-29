using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Planning;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Planning;

[Route("v1/route-step-parameters")]
public class RouteStepParametersController : CrudController<RouteStepParameter, IRouteStepParameterService>
{
    public RouteStepParametersController(IRouteStepParameterService service) : base(service)
    {
    }

}
