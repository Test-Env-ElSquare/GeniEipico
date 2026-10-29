using Microsoft.Data.SqlClient;

namespace Infrastructure.Integration;

internal sealed class EipicoSourceReader(string connectionString)
{
    public async Task<long> GetMaxOrderIdAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT COALESCE(MAX(CONVERT(bigint, Id)), 0) FROM [RealTime].[SAPOrders];", connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<EipicoSapOrderRecord?> ReadDependencyOrderAsync(
        int? orderId, int? materialId, string? batchUid, string? materialUid,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT DISTINCT TOP (2) o.Id
            FROM [RealTime].[SAPOrders] o
            LEFT JOIN [RealTime].[SAPOrderMaterials] m
                ON m.SAPOrderId = o.Id AND m.IsDeleted = 0
            WHERE (@OrderId IS NOT NULL AND o.Id = @OrderId)
               OR (@OrderId IS NULL AND @MaterialId IS NOT NULL AND m.Id = @MaterialId)
               OR (@OrderId IS NULL AND @MaterialId IS NULL AND @BatchUid IS NOT NULL AND o.BatchId = @BatchUid)
               OR (@OrderId IS NULL AND @MaterialId IS NULL AND @BatchUid IS NULL AND @MaterialUid IS NOT NULL AND m.UID = @MaterialUid);
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@OrderId", System.Data.SqlDbType.Int).Value = (object?)orderId ?? DBNull.Value;
        command.Parameters.Add("@MaterialId", System.Data.SqlDbType.Int).Value = (object?)materialId ?? DBNull.Value;
        command.Parameters.Add("@BatchUid", System.Data.SqlDbType.NVarChar, 30).Value = (object?)batchUid ?? DBNull.Value;
        command.Parameters.Add("@MaterialUid", System.Data.SqlDbType.NVarChar, 30).Value = (object?)materialUid ?? DBNull.Value;
        int? foundId = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
                foundId = reader.GetInt32(0);
            if (await reader.ReadAsync(cancellationToken))
                throw new EipicoDependencyNotReadyException("The realtime SAP dependency matches more than one source order.");
        }
        return foundId.HasValue ? await ReadOrderByIdAsync(foundId.Value, cancellationToken) : null;
    }

    public async Task<EipicoOrderLineResolution?> ResolveOrderLineAsync(
        string batchId,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT COUNT(DISTINCT l.Id), MIN(l.Name), MIN(f.Name)
            FROM [RealTime].[MachineLoads] AS ml
            INNER JOIN [Definitions].[Machines] AS m ON m.Id = ml.MachineId
            INNER JOIN [Definitions].[Lines] AS l ON l.Id = m.LineId
            INNER JOIN [Definitions].[Factories] AS f ON f.Id = l.FactoryId
            WHERE ml.BatchId = @BatchId;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(query, connection) { CommandTimeout = 30 };
        command.Parameters.AddWithValue("@BatchId", batchId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        if (reader.GetInt32(0) != 1)
            return null;

        return new EipicoOrderLineResolution(reader.GetString(1), reader.GetString(2));
    }

    public Task<IReadOnlyList<EipicoSapOrderRecord>> ReadOrdersAfterAsync(
        long lastSourceId,
        int batchSize,
        CancellationToken cancellationToken)
        => ReadAsync(
            lastSourceId,
            Math.Clamp(batchSize, 1, 1000),
            ReadMode.AfterSourceId,
            cancellationToken);

    public async Task<EipicoSapOrderRecord?> ReadOrderByIdAsync(
        long sourceId,
        CancellationToken cancellationToken)
        => (await ReadAsync(sourceId, 1, ReadMode.ExactSourceId, cancellationToken)).SingleOrDefault();

    public Task<IReadOnlyList<EipicoSapOrderRecord>> ReadRecentOrdersAsync(
        int batchSize,
        CancellationToken cancellationToken)
        => ReadAsync(
            sourceId: 0,
            Math.Clamp(batchSize, 1, 1000),
            ReadMode.Recent,
            cancellationToken);

    private async Task<IReadOnlyList<EipicoSapOrderRecord>> ReadAsync(
        long sourceId,
        int batchSize,
        ReadMode mode,
        CancellationToken cancellationToken)
    {
        const string selectColumns = """
            SELECT
                o.Id,
                o.BatchId,
                o.JobOrderId,
                o.WarehouseCode,
                o.StorageLocation,
                o.LineCode,
                o.SKUCode,
                o.SKUName,
                o.[TimeStamp],
                m.Id,
                m.Name,
                m.UID,
                m.Quantity,
                m.ItemCode,
                m.OpCode,
                m.Unit
            FROM NextOrders AS o
            LEFT JOIN [RealTime].[SAPOrderMaterials] AS m
                ON m.SAPOrderId = o.Id
               AND m.IsDeleted = 0
            ORDER BY o.Id, m.Id;
            """;

        var query = mode switch
        {
            ReadMode.ExactSourceId => """
              WITH NextOrders AS
              (
                  SELECT TOP (1)
                      Id, BatchId, JobOrderId, WarehouseCode, StorageLocation,
                      LineCode, SKUCode, SKUName, [TimeStamp]
                  FROM [RealTime].[SAPOrders]
                  WHERE Id = @SourceId
                  ORDER BY Id
              )
              """ + selectColumns,
            ReadMode.Recent => """
              WITH NextOrders AS
              (
                  SELECT TOP (@BatchSize)
                      Id, BatchId, JobOrderId, WarehouseCode, StorageLocation,
                      LineCode, SKUCode, SKUName, [TimeStamp]
                  FROM [RealTime].[SAPOrders]
                  ORDER BY Id DESC
              )
              """ + selectColumns,
            _ => """
              WITH NextOrders AS
              (
                  SELECT TOP (@BatchSize)
                      Id, BatchId, JobOrderId, WarehouseCode, StorageLocation,
                      LineCode, SKUCode, SKUName, [TimeStamp]
                  FROM [RealTime].[SAPOrders]
                  WHERE Id > @SourceId
                  ORDER BY Id
              )
              """ + selectColumns
        };

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(query, connection)
        {
            CommandTimeout = 30
        };
        if (mode != ReadMode.Recent)
            command.Parameters.AddWithValue("@SourceId", sourceId);
        if (mode != ReadMode.ExactSourceId)
            command.Parameters.AddWithValue("@BatchSize", batchSize);

        var orders = new Dictionary<int, EipicoSapOrderRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var orderId = reader.GetInt32(0);
            if (!orders.TryGetValue(orderId, out var order))
            {
                order = new EipicoSapOrderRecord
                {
                    Id = orderId,
                    BatchId = GetNullableString(reader, 1),
                    JobOrderId = GetNullableString(reader, 2),
                    WarehouseCode = GetNullableString(reader, 3),
                    StorageLocation = GetNullableString(reader, 4),
                    LineCode = GetNullableString(reader, 5),
                    SkuCode = GetNullableString(reader, 6),
                    SkuName = GetNullableString(reader, 7),
                    ReceivedAt = reader.GetDateTime(8)
                };
                orders.Add(orderId, order);
            }

            if (!reader.IsDBNull(9))
            {
                order.Materials.Add(new EipicoSapMaterialRecord
                {
                    Id = reader.GetInt32(9),
                    Name = GetNullableString(reader, 10),
                    Uid = GetNullableString(reader, 11),
                    Quantity = reader.GetDecimal(12),
                    ItemCode = GetNullableString(reader, 13),
                    OperationCode = GetNullableString(reader, 14),
                    Unit = GetNullableString(reader, 15)
                });
            }
        }

        return orders.Values.OrderBy(x => x.Id).ToArray();
    }

    private enum ReadMode
    {
        AfterSourceId,
        ExactSourceId,
        Recent
    }

    private static string? GetNullableString(SqlDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}

internal sealed record EipicoOrderLineResolution(string LineName, string FactoryName);

internal sealed class EipicoSapOrderRecord
{
    public int Id { get; init; }
    public string? BatchId { get; init; }
    public string? JobOrderId { get; init; }
    public string? WarehouseCode { get; init; }
    public string? StorageLocation { get; init; }
    public string? LineCode { get; init; }
    public string? SkuCode { get; init; }
    public string? SkuName { get; init; }
    public DateTime ReceivedAt { get; init; }
    public List<EipicoSapMaterialRecord> Materials { get; } = [];
}

internal sealed class EipicoSapMaterialRecord
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? Uid { get; init; }
    public decimal Quantity { get; init; }
    public string? ItemCode { get; init; }
    public string? OperationCode { get; init; }
    public string? Unit { get; init; }
}
