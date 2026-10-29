namespace API.Contracts;

public sealed class ScadaMachineDetailQuery
{
    public int? Duration { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
