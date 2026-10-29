using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Maintenance;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Maintenance;

[Route("v1/spare-part-machine-types")]
public class SparePartMachineTypesController : CrudController<SparePartMachineType, ISparePartMachineTypeService>
{
    public SparePartMachineTypesController(ISparePartMachineTypeService service) : base(service)
    {
    }

}
