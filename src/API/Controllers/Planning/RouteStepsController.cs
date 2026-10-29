using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Planning;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Planning;

[Route("v1/route-steps")]
public class RouteStepsController : CrudController<RouteStep, IRouteStepService>
{
    public RouteStepsController(IRouteStepService service) : base(service)
    {
    }

}
