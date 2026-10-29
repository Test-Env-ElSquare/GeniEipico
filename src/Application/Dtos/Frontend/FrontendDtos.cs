namespace Application.Dtos.Frontend;

public sealed record UseCaseLinkDto(
    string Code,
    string Type,
    string Summary,
    string ImplementationMode,
    string FutureNote);

public sealed record UseCaseCoverageDto(
    string Domain,
    string Code,
    string Type,
    string FrontendArea,
    string Endpoint,
    string Controller,
    string ApplicationService,
    string DataLayer,
    string CurrentMode,
    string FutureNote);

public sealed record UiPageDto(
    string Page,
    string Endpoint,
    string EndpointType,
    string BindingMode,
    IReadOnlyList<UseCaseLinkDto> UseCases,
    IReadOnlyDictionary<string, object?> Sections);

public sealed record UiActionResultDto(
    string Type,
    string UseCase,
    string Message,
    object? Data,
    string FutureNote);
