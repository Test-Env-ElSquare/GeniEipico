using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEnergyDashboardReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmsEnergySourceId",
                schema: "MasterData",
                table: "Transformers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmsEnergySourceId",
                schema: "Org",
                table: "ProductionLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmsEnergySourceId",
                schema: "Org",
                table: "Factories",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transformers_EmsEnergySourceId",
                schema: "MasterData",
                table: "Transformers",
                column: "EmsEnergySourceId",
                unique: true,
                filter: "[EmsEnergySourceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionLines_EmsEnergySourceId",
                schema: "Org",
                table: "ProductionLines",
                column: "EmsEnergySourceId",
                unique: true,
                filter: "[EmsEnergySourceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Factories_EmsEnergySourceId",
                schema: "Org",
                table: "Factories",
                column: "EmsEnergySourceId",
                unique: true,
                filter: "[EmsEnergySourceId] IS NOT NULL");

            migrationBuilder.Sql(
                """
                CREATE INDEX [IX_EnergyReadings_EmsDashboard]
                ON [RealTime].[EnergyReadings]
                    ([FactoryId], [ShiftStartTime], [TransformerId], [Timestamp])
                INCLUDE
                    ([TotalEnergyConsumptionDiff],
                     [VoltageL1], [VoltageL2], [VoltageL3],
                     [CurrentL1], [CurrentL2], [CurrentL3],
                     [PowerFactorL1], [PowerFactorL2], [PowerFactorL3],
                     [VoltageThdL1], [VoltageThdL2], [VoltageThdL3],
                     [CurrentThdL1], [CurrentThdL2], [CurrentThdL3])
                WHERE [EmsSourceRecordId] IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                CREATE VIEW [Calculated].[VW_EnergyTransformerCurrent]
                AS
                WITH [CurrentShift] AS
                (
                    SELECT
                        [FactoryId],
                        MAX([ShiftStartTime]) AS [ShiftStartTime]
                    FROM [RealTime].[EnergyReadings]
                    WHERE [EmsSourceRecordId] IS NOT NULL
                      AND [ShiftStartTime] IS NOT NULL
                    GROUP BY [FactoryId]
                )
                SELECT
                    [t].[Id] AS [TransformerId],
                    [t].[EmsEnergySourceId],
                    [t].[TransformerName],
                    [t].[FactoryId],
                    [cs].[ShiftStartTime],
                    CAST(ISNULL(SUM(ISNULL([e].[TotalEnergyConsumptionDiff], 0)), 0) AS decimal(38, 6))
                        AS [TotalEnergyConsumption],
                    CAST(ISNULL(AVG(([e].[PowerFactorL1] + [e].[PowerFactorL2] + [e].[PowerFactorL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [PowerFactor],
                    CAST(ISNULL(AVG(([e].[VoltageL1] + [e].[VoltageL2] + [e].[VoltageL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [Voltage],
                    CAST(ISNULL(AVG(([e].[CurrentL1] + [e].[CurrentL2] + [e].[CurrentL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [Current],
                    CAST(ISNULL(AVG(([e].[VoltageThdL1] + [e].[VoltageThdL2] + [e].[VoltageThdL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [AvgTHDv],
                    CAST(ISNULL(AVG(([e].[CurrentThdL1] + [e].[CurrentThdL2] + [e].[CurrentThdL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [AvgTHDi],
                    MAX([e].[Timestamp]) AS [LastReadingAt],
                    COUNT_BIG([e].[Id]) AS [ReadingCount],
                    CAST(CASE
                        WHEN MAX([e].[Timestamp]) >= DATEADD(HOUR, -3, SYSUTCDATETIME())
                            THEN 'Online'
                        ELSE 'Offline'
                    END AS varchar(7)) AS [Status]
                FROM [MasterData].[Transformers] [t]
                INNER JOIN [CurrentShift] [cs]
                    ON [cs].[FactoryId] = [t].[FactoryId]
                LEFT JOIN [RealTime].[EnergyReadings] [e]
                    ON [e].[TransformerId] = [t].[Id]
                   AND [e].[FactoryId] = [t].[FactoryId]
                   AND [e].[EmsSourceRecordId] IS NOT NULL
                   AND [e].[ShiftStartTime] = [cs].[ShiftStartTime]
                WHERE [t].[EmsEnergySourceId] IS NOT NULL
                  AND [t].[IsDeleted] = 0
                GROUP BY
                    [t].[Id],
                    [t].[EmsEnergySourceId],
                    [t].[TransformerName],
                    [t].[FactoryId],
                    [cs].[ShiftStartTime];
                """);

            migrationBuilder.Sql(
                """
                CREATE VIEW [Calculated].[VW_EnergyTransformerHourly]
                AS
                SELECT
                    [t].[Id] AS [TransformerId],
                    [t].[EmsEnergySourceId],
                    [t].[TransformerName],
                    [e].[FactoryId],
                    [e].[ShiftStartTime],
                    DATEADD(HOUR, DATEDIFF(HOUR, CONVERT(datetime2, '20000101', 112), [e].[Timestamp]), CONVERT(datetime2, '20000101', 112))
                        AS [HourStartTime],
                    CAST(SUM(ISNULL([e].[TotalEnergyConsumptionDiff], 0)) AS decimal(38, 6))
                        AS [TotalEnergyConsumption],
                    CAST(ISNULL(AVG(([e].[PowerFactorL1] + [e].[PowerFactorL2] + [e].[PowerFactorL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [PowerFactor],
                    CAST(ISNULL(AVG(([e].[VoltageL1] + [e].[VoltageL2] + [e].[VoltageL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [Voltage],
                    CAST(ISNULL(AVG(([e].[CurrentL1] + [e].[CurrentL2] + [e].[CurrentL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [Current],
                    CAST(ISNULL(AVG(([e].[VoltageThdL1] + [e].[VoltageThdL2] + [e].[VoltageThdL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [AvgTHDv],
                    CAST(ISNULL(AVG(([e].[CurrentThdL1] + [e].[CurrentThdL2] + [e].[CurrentThdL3]) / 3.0), 0) AS decimal(28, 6))
                        AS [AvgTHDi],
                    MAX([e].[Timestamp]) AS [LastReadingAt],
                    COUNT_BIG([e].[Id]) AS [ReadingCount]
                FROM [RealTime].[EnergyReadings] [e]
                INNER JOIN [MasterData].[Transformers] [t]
                    ON [t].[Id] = [e].[TransformerId]
                   AND [t].[EmsEnergySourceId] IS NOT NULL
                   AND [t].[IsDeleted] = 0
                WHERE [e].[EmsSourceRecordId] IS NOT NULL
                GROUP BY
                    [t].[Id],
                    [t].[EmsEnergySourceId],
                    [t].[TransformerName],
                    [e].[FactoryId],
                    [e].[ShiftStartTime],
                    DATEADD(HOUR, DATEDIFF(HOUR, CONVERT(datetime2, '20000101', 112), [e].[Timestamp]), CONVERT(datetime2, '20000101', 112));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS [Calculated].[VW_EnergyTransformerHourly];");
            migrationBuilder.Sql("DROP VIEW IF EXISTS [Calculated].[VW_EnergyTransformerCurrent];");
            migrationBuilder.Sql("DROP INDEX IF EXISTS [IX_EnergyReadings_EmsDashboard] ON [RealTime].[EnergyReadings];");

            migrationBuilder.DropIndex(
                name: "IX_Transformers_EmsEnergySourceId",
                schema: "MasterData",
                table: "Transformers");

            migrationBuilder.DropIndex(
                name: "IX_ProductionLines_EmsEnergySourceId",
                schema: "Org",
                table: "ProductionLines");

            migrationBuilder.DropIndex(
                name: "IX_Factories_EmsEnergySourceId",
                schema: "Org",
                table: "Factories");

            migrationBuilder.DropColumn(
                name: "EmsEnergySourceId",
                schema: "MasterData",
                table: "Transformers");

            migrationBuilder.DropColumn(
                name: "EmsEnergySourceId",
                schema: "Org",
                table: "ProductionLines");

            migrationBuilder.DropColumn(
                name: "EmsEnergySourceId",
                schema: "Org",
                table: "Factories");
        }
    }
}
