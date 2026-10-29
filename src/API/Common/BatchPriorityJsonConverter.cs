using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Enums;

namespace API.Common;

public sealed class BatchPriorityJsonConverter : JsonConverter<BatchPriority>
{
    public override BatchPriority Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number ||
            !reader.TryGetInt32(out var value) ||
            !Enum.IsDefined(typeof(BatchPriority), value))
        {
            throw new JsonException("Batch priority must be 1 (Low), 2 (Normal), or 3 (High).");
        }

        return (BatchPriority)value;
    }

    public override void Write(
        Utf8JsonWriter writer,
        BatchPriority value,
        JsonSerializerOptions options)
        => writer.WriteNumberValue((int)value);
}
