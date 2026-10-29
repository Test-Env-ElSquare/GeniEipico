using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.IoT;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.IoT;

[Route("v1/device-type-tag-definitions")]
public class DeviceTypeTagDefinitionsController : CrudController<DeviceTypeTagDefinition, IDeviceTypeTagDefinitionService>
{
    public DeviceTypeTagDefinitionsController(IDeviceTypeTagDefinitionService service) : base(service)
    {
    }

}
