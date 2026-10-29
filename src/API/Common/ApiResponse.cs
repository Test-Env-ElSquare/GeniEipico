namespace API.Common;

public sealed record ApiError(string Code, string Message, IReadOnlyList<string>? Details = null);

public sealed record ApiResponse<T>(bool Success, T? Data, object? Meta, ApiError? Error, DateTime Ts)
{
    public static ApiResponse<T> Ok(T? data, object? meta = null)
    {
        return new ApiResponse<T>(true, data, meta, null, DateTime.UtcNow);
    }

    public static ApiResponse<T> Fail(string code, string message, IReadOnlyList<string>? details = null)
    {
        return new ApiResponse<T>(false, default, null, new ApiError(code, message, details), DateTime.UtcNow);
    }
}
