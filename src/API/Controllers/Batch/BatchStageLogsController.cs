using API.Common;
using Application.Abstractions.Services;
using Domain.Entities.Production;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers.Batch;

[Route("v1/batch-stage-logs")]
public class BatchStageLogsController : CrudController<BatchStageLog, IBatchStageLogService>
{
    public BatchStageLogsController(IBatchStageLogService service) : base(service)
    {
    }

}
