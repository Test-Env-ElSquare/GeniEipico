using API.Common;
using Application.Abstractions.Repositories;
using Application.Abstractions.Services;
using Domain.Entities.Planning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Planning;

[Route("v1/shifts")]
public class ShiftsController : CrudController<Shift, IShiftService>
{
    private readonly IUnitOfWork _unitOfWork;

    public ShiftsController(IShiftService service, IUnitOfWork unitOfWork) : base(service)
    {
        _unitOfWork = unitOfWork;
    }

    [Authorize]
    [HttpGet("by-factory/{factoryId:int}")]
    public IActionResult GetByFactory(int factoryId)
    {
        if (!AuthenticatedAccessScope.TryResolve(
                User,
                _unitOfWork,
                out var scope,
                out var errorCode,
                out var errorMessage))
        {
            return Unauthorized(ApiResponse<object>.Fail(errorCode, errorMessage));
        }

        if (factoryId <= 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "factory_required",
                "FactoryId must be greater than zero."));
        }

        if (!scope.CanAccessFactory(factoryId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                "factory_forbidden",
                "The selected factory does not belong to the authenticated customer or is outside the user's access scope."));
        }

        var shifts = Service.Query()
            .Where(x => x.IsActive &&
                        !x.IsDeleted &&
                        (x.FactoryId == factoryId ||
                         x.FactoryLinks.Any(link => link.FactoryId == factoryId)))
            .OrderBy(x => x.StartTime)
            .Select(x => new
            {
                x.Id,
                FactoryId = factoryId,
                x.Name,
                x.StartTime,
                x.EndTime,
                x.IsActive
            })
            .ToList<object>();

        return Ok(ApiResponse<IReadOnlyList<object>>.Ok(shifts));
    }


    // Endpoint Type D: Read Model / View. Returns current shift; simple query/view later.
    [HttpGet("current")]
    public IActionResult Current([FromQuery] int factoryId)
    {
        return StaticOk("/v1/shifts/current", new { factoryId });
    }

}
