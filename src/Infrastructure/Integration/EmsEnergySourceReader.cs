using Microsoft.Data.SqlClient;

namespace Infrastructure.Integration;

/// <summary>Read-only access to the EMS definitions and raw Energy stream.</summary>
public sealed class EmsEnergySourceReader(string connectionString)
{
    public async Task<long> GetMaxEnergyIdAsync(CancellationToken cancellationToken)
    {
        const string query = "SELECT COALESCE(MAX(CONVERT(bigint, Id)), 0) FROM [RealTime].[Energy];";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection) { CommandTimeout = 30 };
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<EmsDefinitionSnapshot> ReadDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT Id, Name, Location, CreatedAt, IsDeleted
            FROM [Definitions].[Factories]
            ORDER BY Id;

            SELECT Id, TransformerName, FactoryId, CreatedAt, IsDeleted
            FROM [Definitions].[Transformer]
            ORDER BY Id;

            SELECT Id, Name, FactoryId, RatioFromParent, TransformerId, CreatedAt, IsDeleted
            FROM [Definitions].[Zones]
            ORDER BY Id;

            SELECT Id, Name, Number, Type, FactoryId, Area, ViewName,
                   RatioFromParent, ZoneId, CreatedAt, IsDeleted
            FROM [Definitions].[Lines]
            ORDER BY Id;

            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection) { CommandTimeout = 60 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var snapshot = new EmsDefinitionSnapshot();

        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Factories.Add(new EmsFactorySourceDefinition
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Location = reader.GetString(2),
                CreatedAt = reader.GetDateTime(3),
                IsDeleted = reader.GetBoolean(4)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Transformers.Add(new EmsTransformerSourceDefinition
            {
                Id = reader.GetInt32(0),
                TransformerName = reader.GetString(1),
                FactoryId = GetNullableInt32(reader, 2),
                CreatedAt = reader.GetDateTime(3),
                IsDeleted = reader.GetBoolean(4)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Zones.Add(new EmsZoneSourceDefinition
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                FactoryId = reader.GetInt32(2),
                RatioFromParent = GetNullableDecimal(reader, 3),
                TransformerId = GetNullableInt32(reader, 4),
                CreatedAt = reader.GetDateTime(5),
                IsDeleted = reader.GetBoolean(6)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Lines.Add(new EmsLineSourceDefinition
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Number = reader.GetInt32(2),
                Type = GetNullableString(reader, 3),
                FactoryId = GetNullableInt32(reader, 4),
                Area = reader.GetString(5),
                ViewName = GetNullableString(reader, 6),
                RatioFromParent = GetNullableDecimal(reader, 7),
                ZoneId = GetNullableInt32(reader, 8),
                CreatedAt = reader.GetDateTime(9),
                IsDeleted = reader.GetBoolean(10)
            });
        }

        return snapshot;
    }

    public async Task<IReadOnlyList<EmsEnergySourceRecord>> ReadEnergyAfterAsync(
        long lastSourceId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT TOP (@BatchSize)
                Id, SourceId,
                V1, V2, V3, I1, I2, I3,
                PF1, PF2, PF3,
                Papp1, Papp2, Papp3,
                Pact1, Pact2, Pact3,
                Preact1, Preact2, Preact3,
                Energy1, Energy2, Energy3,
                DiffEnergy1, DiffEnergy2, DiffEnergy3,
                THDv1, THDv2, THDv3,
                THDi1, THDi2, THDi3,
                Fault, [TimeStamp], ShiftStartTime,
                AvgVoltage, AvgCurrent, AvgPowerFactor,
                AvgPact, AvgPreact, AvgPapp, SumEnergy,
                AvgTHDi, AvgTHDv, TotalEnergyConsumptionDiff,
                CreatedAt, IsDeleted
            FROM [RealTime].[Energy]
            WHERE Id > @LastSourceId
            ORDER BY Id;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection) { CommandTimeout = 30 };
        command.Parameters.AddWithValue("@BatchSize", Math.Clamp(batchSize, 1, 5000));
        command.Parameters.AddWithValue("@LastSourceId", lastSourceId);

        var records = new List<EmsEnergySourceRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new EmsEnergySourceRecord
            {
                Id = reader.GetInt32(0),
                SourceId = reader.GetString(1),
                V1 = reader.GetDecimal(2),
                V2 = reader.GetDecimal(3),
                V3 = reader.GetDecimal(4),
                I1 = reader.GetDecimal(5),
                I2 = reader.GetDecimal(6),
                I3 = reader.GetDecimal(7),
                PF1 = reader.GetDecimal(8),
                PF2 = reader.GetDecimal(9),
                PF3 = reader.GetDecimal(10),
                Papp1 = reader.GetDecimal(11),
                Papp2 = reader.GetDecimal(12),
                Papp3 = reader.GetDecimal(13),
                Pact1 = reader.GetDecimal(14),
                Pact2 = reader.GetDecimal(15),
                Pact3 = reader.GetDecimal(16),
                Preact1 = reader.GetDecimal(17),
                Preact2 = reader.GetDecimal(18),
                Preact3 = reader.GetDecimal(19),
                Energy1 = reader.GetDecimal(20),
                Energy2 = reader.GetDecimal(21),
                Energy3 = reader.GetDecimal(22),
                DiffEnergy1 = GetNullableDecimal(reader, 23),
                DiffEnergy2 = GetNullableDecimal(reader, 24),
                DiffEnergy3 = GetNullableDecimal(reader, 25),
                THDv1 = reader.GetDecimal(26),
                THDv2 = reader.GetDecimal(27),
                THDv3 = reader.GetDecimal(28),
                THDi1 = reader.GetDecimal(29),
                THDi2 = reader.GetDecimal(30),
                THDi3 = reader.GetDecimal(31),
                Fault = GetNullableInt32(reader, 32),
                TimeStamp = reader.GetDateTime(33),
                ShiftStartTime = reader.GetDateTime(34),
                AvgVoltage = reader.GetDecimal(35),
                AvgCurrent = reader.GetDecimal(36),
                AvgPowerFactor = reader.GetDecimal(37),
                AvgPact = reader.GetDecimal(38),
                AvgPreact = reader.GetDecimal(39),
                AvgPapp = reader.GetDecimal(40),
                SumEnergy = reader.GetDecimal(41),
                AvgTHDi = reader.GetDecimal(42),
                AvgTHDv = reader.GetDecimal(43),
                TotalEnergyConsumptionDiff = GetNullableDecimal(reader, 44),
                CreatedAt = reader.GetDateTime(45),
                IsDeleted = reader.GetBoolean(46)
            });
        }

        return records;
    }

    private static string? GetNullableString(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? GetNullableInt32(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);

    private static decimal? GetNullableDecimal(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
}

public sealed class EmsDefinitionSnapshot
{
    public List<EmsFactorySourceDefinition> Factories { get; } = [];
    public List<EmsTransformerSourceDefinition> Transformers { get; } = [];
    public List<EmsZoneSourceDefinition> Zones { get; } = [];
    public List<EmsLineSourceDefinition> Lines { get; } = [];
}

public sealed class EmsFactorySourceDefinition
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public bool IsDeleted { get; init; }
}

public sealed class EmsTransformerSourceDefinition
{
    public int Id { get; init; }
    public string TransformerName { get; init; } = string.Empty;
    public int? FactoryId { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsDeleted { get; init; }
}

public sealed class EmsZoneSourceDefinition
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int FactoryId { get; init; }
    public decimal? RatioFromParent { get; init; }
    public int? TransformerId { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsDeleted { get; init; }
}

public sealed class EmsLineSourceDefinition
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Number { get; init; }
    public string? Type { get; init; }
    public int? FactoryId { get; init; }
    public string Area { get; init; } = string.Empty;
    public string? ViewName { get; init; }
    public decimal? RatioFromParent { get; init; }
    public int? ZoneId { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsDeleted { get; init; }
}

public sealed class EmsEnergySourceRecord
{
    public int Id { get; init; }
    public string SourceId { get; init; } = string.Empty;
    public decimal V1 { get; init; }
    public decimal V2 { get; init; }
    public decimal V3 { get; init; }
    public decimal I1 { get; init; }
    public decimal I2 { get; init; }
    public decimal I3 { get; init; }
    public decimal PF1 { get; init; }
    public decimal PF2 { get; init; }
    public decimal PF3 { get; init; }
    public decimal Papp1 { get; init; }
    public decimal Papp2 { get; init; }
    public decimal Papp3 { get; init; }
    public decimal Pact1 { get; init; }
    public decimal Pact2 { get; init; }
    public decimal Pact3 { get; init; }
    public decimal Preact1 { get; init; }
    public decimal Preact2 { get; init; }
    public decimal Preact3 { get; init; }
    public decimal Energy1 { get; init; }
    public decimal Energy2 { get; init; }
    public decimal Energy3 { get; init; }
    public decimal? DiffEnergy1 { get; init; }
    public decimal? DiffEnergy2 { get; init; }
    public decimal? DiffEnergy3 { get; init; }
    public decimal THDv1 { get; init; }
    public decimal THDv2 { get; init; }
    public decimal THDv3 { get; init; }
    public decimal THDi1 { get; init; }
    public decimal THDi2 { get; init; }
    public decimal THDi3 { get; init; }
    public int? Fault { get; init; }
    public DateTime TimeStamp { get; init; }
    public DateTime ShiftStartTime { get; init; }
    public decimal AvgVoltage { get; init; }
    public decimal AvgCurrent { get; init; }
    public decimal AvgPowerFactor { get; init; }
    public decimal AvgPact { get; init; }
    public decimal AvgPreact { get; init; }
    public decimal AvgPapp { get; init; }
    public decimal SumEnergy { get; init; }
    public decimal AvgTHDi { get; init; }
    public decimal AvgTHDv { get; init; }
    public decimal? TotalEnergyConsumptionDiff { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsDeleted { get; init; }
}
