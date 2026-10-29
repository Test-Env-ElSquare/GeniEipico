namespace API.Common;

public sealed record StaticEndpointResult(
    string Endpoint,
    string Message,
    object? Parameters = null,
    string Status = "Static endpoint created - business logic pending");
