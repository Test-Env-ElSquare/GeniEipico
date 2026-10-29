using Microsoft.Data.SqlClient;

namespace Infrastructure.Integration;

internal sealed class EipicoDefinitionSourceReader(string connectionString)
{
    public async Task<EipicoDefinitionSnapshot> ReadSnapshotAsync(
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT Id, Name, Location
            FROM [Definitions].[Factories]
            ORDER BY Id;

            SELECT Id, Name, AliasName, Number, Type, FactoryId, Area, ViewName
            FROM [Definitions].[Lines]
            ORDER BY Id;

            SELECT Id, Name, Faunctionality, RatedSpeed, LineId, UID, UnitOfSpeed,
                   Building, Floor, Room, IsDominant, Factor
            FROM [Definitions].[Machines]
            ORDER BY Id;

            SELECT Id, Name, ItemCode
            FROM [Definitions].[Materials]
            ORDER BY Id;

            SELECT Id, Name, TechnicalSKU, LineId, PackSize, CartonSize, PallSize,
                   Size, Unit, SkuCode
            FROM [Definitions].[SKUs]
            ORDER BY Id;

            SELECT s.Id, s.Name, s.FactoryId, s.Room, s.CurrentScaleTypeId,
                   st.Description
            FROM [Definitions].[Scales] AS s
            LEFT JOIN [Definitions].[ScaleTypes] AS st
                ON st.Id = s.CurrentScaleTypeId
            ORDER BY Id;

            SELECT Id, SourceId, TransformerName, FactoryId
            FROM [Definitions].[Transformers]
            ORDER BY Id;

            SELECT LineId, TransformerId
            FROM [Definitions].[LineTransformers]
            ORDER BY LineId, TransformerId;

            SELECT Id, DisplayName, ColumnName, ChartType
            FROM [Definitions].[Tag]
            ORDER BY Id;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection)
        {
            CommandTimeout = 60
        };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var snapshot = new EipicoDefinitionSnapshot();
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Factories.Add(new EipicoFactoryDefinition
            {
                Id = reader.GetInt32(0),
                Name = GetNullableString(reader, 1),
                Location = GetNullableString(reader, 2)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Lines.Add(new EipicoLineDefinition
            {
                Id = reader.GetInt32(0),
                Name = GetNullableString(reader, 1),
                AliasName = GetNullableString(reader, 2),
                Number = GetNullableInt32(reader, 3),
                Type = GetNullableString(reader, 4),
                FactoryId = GetNullableInt32(reader, 5),
                Area = GetNullableString(reader, 6),
                ViewName = GetNullableString(reader, 7)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Machines.Add(new EipicoMachineDefinition
            {
                Id = reader.GetInt32(0),
                Name = GetNullableString(reader, 1),
                Functionality = GetNullableString(reader, 2),
                RatedSpeed = GetNullableDecimal(reader, 3),
                LineId = GetNullableInt32(reader, 4),
                Uid = GetNullableString(reader, 5),
                UnitOfSpeed = GetNullableString(reader, 6),
                Building = GetNullableString(reader, 7),
                Floor = GetNullableString(reader, 8),
                Room = GetNullableString(reader, 9),
                IsDominant = GetNullableBoolean(reader, 10),
                Factor = GetNullableDecimal(reader, 11)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Materials.Add(new EipicoMaterialDefinition
            {
                Id = reader.GetInt32(0),
                Name = GetNullableString(reader, 1),
                ItemCode = GetNullableString(reader, 2)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Skus.Add(new EipicoSkuDefinition
            {
                Id = reader.GetInt32(0),
                Name = GetNullableString(reader, 1),
                TechnicalSku = GetNullableInt32(reader, 2),
                LineId = GetNullableInt32(reader, 3),
                PackSize = GetNullableInt32(reader, 4),
                CartonSize = GetNullableInt32(reader, 5),
                PallSize = GetNullableInt32(reader, 6),
                Size = GetNullableDecimal(reader, 7),
                Unit = GetNullableString(reader, 8),
                SkuCode = GetNullableString(reader, 9)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Scales.Add(new EipicoScaleDefinition
            {
                Id = reader.GetInt32(0),
                Name = GetNullableString(reader, 1),
                FactoryId = GetNullableInt32(reader, 2),
                Room = GetNullableString(reader, 3),
                CurrentScaleTypeId = GetNullableInt32(reader, 4),
                ScaleTypeDescription = GetNullableString(reader, 5)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Transformers.Add(new EipicoTransformerDefinition
            {
                Id = reader.GetInt32(0),
                SourceId = GetNullableString(reader, 1),
                Name = GetNullableString(reader, 2),
                FactoryId = GetNullableInt32(reader, 3)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.LineTransformers.Add(new EipicoLineTransformerDefinition
            {
                LineId = reader.GetInt32(0),
                TransformerId = reader.GetInt32(1)
            });
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            snapshot.Tags.Add(new EipicoTagDefinition
            {
                Id = reader.GetInt32(0),
                DisplayName = GetNullableString(reader, 1),
                ColumnName = GetNullableString(reader, 2),
                ChartType = GetNullableString(reader, 3)
            });
        }

        return snapshot;
    }

    private static string? GetNullableString(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static int? GetNullableInt32(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));

    private static decimal? GetNullableDecimal(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));

    private static bool? GetNullableBoolean(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
}

internal sealed class EipicoDefinitionSnapshot
{
    public List<EipicoFactoryDefinition> Factories { get; } = [];
    public List<EipicoLineDefinition> Lines { get; } = [];
    public List<EipicoMachineDefinition> Machines { get; } = [];
    public List<EipicoMaterialDefinition> Materials { get; } = [];
    public List<EipicoSkuDefinition> Skus { get; } = [];
    public List<EipicoScaleDefinition> Scales { get; } = [];
    public List<EipicoTransformerDefinition> Transformers { get; } = [];
    public List<EipicoLineTransformerDefinition> LineTransformers { get; } = [];
    public List<EipicoTagDefinition> Tags { get; } = [];
}

internal sealed class EipicoFactoryDefinition
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Location { get; init; }
}

internal sealed class EipicoLineDefinition
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? AliasName { get; init; }
    public int? Number { get; init; }
    public string? Type { get; init; }
    public int? FactoryId { get; init; }
    public string? Area { get; init; }
    public string? ViewName { get; init; }
}

internal sealed class EipicoMachineDefinition
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Functionality { get; init; }
    public decimal? RatedSpeed { get; init; }
    public int? LineId { get; init; }
    public string? Uid { get; init; }
    public string? UnitOfSpeed { get; init; }
    public string? Building { get; init; }
    public string? Floor { get; init; }
    public string? Room { get; init; }
    public bool? IsDominant { get; init; }
    public decimal? Factor { get; init; }
}

internal sealed class EipicoMaterialDefinition
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? ItemCode { get; init; }
}

internal sealed class EipicoSkuDefinition
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public int? TechnicalSku { get; init; }
    public int? LineId { get; init; }
    public int? PackSize { get; init; }
    public int? CartonSize { get; init; }
    public int? PallSize { get; init; }
    public decimal? Size { get; init; }
    public string? Unit { get; init; }
    public string? SkuCode { get; init; }
}

internal sealed class EipicoScaleDefinition
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public int? FactoryId { get; init; }
    public string? Room { get; init; }
    public int? CurrentScaleTypeId { get; init; }
    public string? ScaleTypeDescription { get; init; }
}

internal sealed class EipicoTransformerDefinition
{
    public int Id { get; init; }
    public string? SourceId { get; init; }
    public string? Name { get; init; }
    public int? FactoryId { get; init; }
}

internal sealed class EipicoLineTransformerDefinition
{
    public int LineId { get; init; }
    public int TransformerId { get; init; }
}

internal sealed class EipicoTagDefinition
{
    public int Id { get; init; }
    public string? DisplayName { get; init; }
    public string? ColumnName { get; init; }
    public string? ChartType { get; init; }
}
