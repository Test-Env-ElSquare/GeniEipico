namespace API.Contracts;

public sealed class OperationFilterQuery
{
    public int? FactoryId { get; set; }
    public int? LineId { get; set; }
    public int Duration { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? MachineId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
