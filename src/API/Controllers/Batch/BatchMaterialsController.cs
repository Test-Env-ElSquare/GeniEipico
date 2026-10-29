using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Production;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Batch;

[Route("v1/batch-materials")]
public class BatchMaterialsController : CrudController<BatchMaterial, IBatchMaterialService>
{
    public BatchMaterialsController(IBatchMaterialService service) : base(service)
    {
    }

}
