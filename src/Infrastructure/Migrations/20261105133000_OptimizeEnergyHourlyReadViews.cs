using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(MesDbContext))]
[Migration("20261105133000_OptimizeEnergyHourlyReadViews")]
public partial class OptimizeEnergyHourlyReadViews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [Calculated].[VW_EnergyZoneHourly]
            AS
            SELECT
                [z].[Id] AS [ZoneId],
                [z].[EmsSourceId] AS [EmsEnergySourceId],
                [z].[Name] AS [ZoneName],
                [f].[Id] AS [FactoryId],
                [t].[Id] AS [TransformerId],
                CONVERT(datetime2,
                    ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                    AS [ShiftStartTime],
                CONVERT(datetime2,
                    ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                    AS [HourStartTime],
                CAST([source].[EnergyConsumption] AS decimal(38, 6)) AS [EnergyConsumption],
                CAST([source].[Status] AS varchar(7)) AS [Status]
            FROM [EMS].[Calculated].[ZoneHourlyAnalysisCurrent] AS [source]
            INNER JOIN [Org].[EnergyZones] AS [z]
                ON [z].[EmsSourceId] = [source].[ZoneId]
               AND [z].[IsDeleted] = 0
            INNER JOIN [Org].[Factories] AS [f]
                ON [f].[Id] = [z].[FactoryId]
               AND [f].[EmsEnergySourceId] = [source].[FactoryId]
               AND [f].[IsDeleted] = 0
            INNER JOIN [MasterData].[Transformers] AS [t]
                ON [t].[Id] = [z].[TransformerId]
               AND [t].[EmsEnergySourceId] = [source].[TransformerId]
               AND [t].[FactoryId] = [f].[Id]
               AND [t].[IsDeleted] = 0;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [Calculated].[VW_EnergyLineHourly]
            AS
            SELECT
                [line].[Id] AS [LineId],
                [line].[EmsEnergySourceId],
                [line].[Name] AS [LineName],
                [zone].[Id] AS [ZoneId],
                [zone].[TransformerId],
                [factory].[Id] AS [FactoryId],
                CONVERT(datetime2,
                    ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                    AS [ShiftStartTime],
                CONVERT(datetime2,
                    ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                    AS [HourStartTime],
                CAST(ISNULL([source].[Availability], 0) AS decimal(18, 10)) AS [Availability],
                CAST(ISNULL([source].[AdjustedAvailability], 0) AS decimal(18, 10)) AS [AdjustedAvailability],
                CAST(ISNULL([source].[EnergyConsumption], 0) AS decimal(38, 6)) AS [EnergyConsumption],
                CAST([source].[Status] AS varchar(7)) AS [Status]
            FROM [EMS].[Calculated].[LineHourlyAnalysisCurrent] AS [source]
            INNER JOIN [Org].[ProductionLines] AS [line]
                ON [line].[EmsEnergySourceId] = [source].[LineId]
               AND [line].[IsDeleted] = 0
            INNER JOIN [Org].[Factories] AS [factory]
                ON [factory].[Id] = [line].[FactoryId]
               AND [factory].[EmsEnergySourceId] = [source].[FactoryId]
               AND [factory].[IsDeleted] = 0
            LEFT JOIN [Org].[EnergyZones] AS [zone]
                ON [zone].[Id] = [line].[EnergyZoneId]
               AND [zone].[EmsSourceId] = [source].[ZoneId]
               AND [zone].[FactoryId] = [factory].[Id]
               AND [zone].[IsDeleted] = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [Calculated].[VW_EnergyZoneHourly]
            AS
            SELECT
                [z].[Id] AS [ZoneId],
                [z].[EmsSourceId] AS [EmsEnergySourceId],
                [z].[Name] AS [ZoneName],
                [f].[Id] AS [FactoryId],
                [t].[Id] AS [TransformerId],
                CONVERT(datetime2,
                    ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                    AS [ShiftStartTime],
                CONVERT(datetime2,
                    ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                    AS [HourStartTime],
                CAST([source].[EnergyConsumption] AS decimal(38, 6)) AS [EnergyConsumption],
                CAST([source].[Status] AS varchar(7)) AS [Status]
            FROM [EMS].[Calculated].[VW_ZoneHourlyAnalysis] AS [source]
            INNER JOIN [Org].[EnergyZones] AS [z]
                ON [z].[EmsSourceId] = [source].[ZoneId]
               AND [z].[IsDeleted] = 0
            INNER JOIN [Org].[Factories] AS [f]
                ON [f].[Id] = [z].[FactoryId]
               AND [f].[EmsEnergySourceId] = [source].[FactoryId]
               AND [f].[IsDeleted] = 0
            INNER JOIN [MasterData].[Transformers] AS [t]
                ON [t].[Id] = [z].[TransformerId]
               AND [t].[EmsEnergySourceId] = [source].[TransformerId]
               AND [t].[FactoryId] = [f].[Id]
               AND [t].[IsDeleted] = 0;
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [Calculated].[VW_EnergyLineHourly]
            AS
            SELECT
                [line].[Id] AS [LineId],
                [line].[EmsEnergySourceId],
                [line].[Name] AS [LineName],
                [zone].[Id] AS [ZoneId],
                [zone].[TransformerId],
                [factory].[Id] AS [FactoryId],
                CONVERT(datetime2,
                    ([source].[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                    AS [ShiftStartTime],
                CONVERT(datetime2,
                    ([source].[HourStartTime] AT TIME ZONE 'Egypt Standard Time') AT TIME ZONE 'UTC')
                    AS [HourStartTime],
                CAST(ISNULL([source].[Availability], 0) AS decimal(18, 10)) AS [Availability],
                CAST(ISNULL([source].[AdjustedAvailability], 0) AS decimal(18, 10)) AS [AdjustedAvailability],
                CAST(ISNULL([source].[EnergyConsumption], 0) AS decimal(38, 6)) AS [EnergyConsumption],
                CAST([source].[Status] AS varchar(7)) AS [Status]
            FROM [EMS].[Calculated].[VW_LineHourlyAnalysis] AS [source]
            INNER JOIN [Org].[ProductionLines] AS [line]
                ON [line].[EmsEnergySourceId] = [source].[LineId]
               AND [line].[IsDeleted] = 0
            INNER JOIN [Org].[Factories] AS [factory]
                ON [factory].[Id] = [line].[FactoryId]
               AND [factory].[EmsEnergySourceId] = [source].[FactoryId]
               AND [factory].[IsDeleted] = 0
            LEFT JOIN [Org].[EnergyZones] AS [zone]
                ON [zone].[Id] = [line].[EnergyZoneId]
               AND [zone].[EmsSourceId] = [source].[ZoneId]
               AND [zone].[FactoryId] = [factory].[Id]
               AND [zone].[IsDeleted] = 0;
            """);
    }
}
