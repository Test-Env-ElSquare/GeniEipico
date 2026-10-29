namespace Application.Common.Workflow;

public sealed class WorkflowResult<T>
{
    private WorkflowResult(bool succeeded, T? data, string? errorCode, string? message)
    {
        Succeeded = succeeded;
        Data = data;
        ErrorCode = errorCode;
        Message = message;
    }

    public bool Succeeded { get; }
    public T? Data { get; }
    public string? ErrorCode { get; }
    public string? Message { get; }

    public static WorkflowResult<T> Ok(T data, string? message = null)
        => new(true, data, null, message);

    public static WorkflowResult<T> Fail(string errorCode, string message)
        => new(false, default, errorCode, message);
}
