using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Production;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Batch;

[Route("v1/batch-uid-sequences")]
public class BatchUidSequencesController : CrudController<BatchUidSequence, IBatchUidSequenceService>
{
    public BatchUidSequencesController(IBatchUidSequenceService service) : base(service)
    {
    }

}
