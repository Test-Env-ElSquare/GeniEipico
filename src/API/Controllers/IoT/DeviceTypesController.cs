using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.IoT;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.IoT;

[Route("v1/device-types")]
public class DeviceTypesController : CrudController<DeviceType, IDeviceTypeService>
{
    public DeviceTypesController(IDeviceTypeService service) : base(service)
    {
    }

}
