using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateEmsEnergyIntoDomain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EnergyRatioFromParent",
                schema: "Org",
                table: "ProductionLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EnergyZoneId",
                schema: "Org",
                table: "ProductionLines",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EmsSourceRecordId",
                schema: "Analytics",
                table: "EnergyReadings",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnergyDiffL1",
                schema: "Analytics",
                table: "EnergyReadings",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnergyDiffL2",
                schema: "Analytics",
                table: "EnergyReadings",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EnergyDiffL3",
                schema: "Analytics",
                table: "EnergyReadings",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShiftStartTime",
                schema: "Analytics",
                table: "EnergyReadings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalEnergyConsumptionDiff",
                schema: "Analytics",
                table: "EnergyReadings",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnergyZones",
                schema: "Org",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmsSourceId = table.Column<int>(type: "int", nullable: false),
                    FactoryId = table.Column<int>(type: "int", nullable: false),
                    TransformerId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RatioFromParent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SourceCreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnergyZones_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "Org",
                        principalTable: "Factories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnergyZones_Transformers_TransformerId",
                        column: x => x.TransformerId,
                        principalSchema: "MasterData",
                        principalTable: "Transformers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionLines_EnergyZoneId",
                schema: "Org",
                table: "ProductionLines",
                column: "EnergyZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyReadings_EmsSourceRecordId",
                schema: "Analytics",
                table: "EnergyReadings",
                column: "EmsSourceRecordId",
                unique: true,
                filter: "[EmsSourceRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyZones_FactoryId_EmsSourceId",
                schema: "Org",
                table: "EnergyZones",
                columns: new[] { "FactoryId", "EmsSourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnergyZones_FactoryId_Name",
                schema: "Org",
                table: "EnergyZones",
                columns: new[] { "FactoryId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_EnergyZones_TransformerId",
                schema: "Org",
                table: "EnergyZones",
                column: "TransformerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionLines_EnergyZones_EnergyZoneId",
                schema: "Org",
                table: "ProductionLines",
                column: "EnergyZoneId",
                principalSchema: "Org",
                principalTable: "EnergyZones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Consolidate the verified EMS mirror into the existing Genie
            // domain tables before the redundant mirror tables are removed.
            migrationBuilder.Sql(
                """
                SET XACT_ABORT ON;

                INSERT INTO [Org].[EnergyZones]
                    ([EmsSourceId], [FactoryId], [TransformerId], [Name],
                     [RatioFromParent], [SourceCreatedAt], [IsDeleted])
                SELECT z.[Id], f.[Id], tr.[Id], z.[Name], z.[RatioFromParent],
                       z.[CreatedAt], z.[IsDeleted]
                FROM [Integration].[EmsZones] z
                INNER JOIN [Integration].[EmsFactories] sf
                    ON sf.[Id] = z.[FactoryId]
                INNER JOIN [Org].[Factories] f
                    ON f.[IsDeleted] = 0 AND f.[Name] = sf.[Name]
                LEFT JOIN [Integration].[EmsTransformers] st
                    ON st.[Id] = z.[TransformerId]
                LEFT JOIN [MasterData].[Transformers] tr
                    ON tr.[IsDeleted] = 0
                   AND tr.[FactoryId] = f.[Id]
                   AND tr.[TransformerName] = st.[TransformerName];

                IF ((SELECT COUNT(*) FROM [Org].[EnergyZones]) <>
                    (SELECT COUNT(*) FROM [Integration].[EmsZones]))
                    THROW 51001, 'EMS zone consolidation count mismatch.', 1;

                UPDATE pl
                SET pl.[EnergyZoneId] = ez.[Id],
                    pl.[EnergyRatioFromParent] = sl.[RatioFromParent]
                FROM [Integration].[EmsLines] sl
                INNER JOIN [Integration].[EmsFactories] sf
                    ON sf.[Id] = sl.[FactoryId]
                INNER JOIN [Org].[Factories] f
                    ON f.[IsDeleted] = 0 AND f.[Name] = sf.[Name]
                INNER JOIN [Org].[ProductionLines] pl
                    ON pl.[IsDeleted] = 0
                   AND pl.[FactoryId] = f.[Id]
                   AND pl.[Code] = sl.[Name]
                LEFT JOIN [Org].[EnergyZones] ez
                    ON ez.[EmsSourceId] = sl.[ZoneId];

                IF ((SELECT COUNT(*)
                     FROM [Integration].[EmsLines] sl
                     INNER JOIN [Integration].[EmsFactories] sf ON sf.[Id] = sl.[FactoryId]
                     INNER JOIN [Org].[Factories] f
                        ON f.[IsDeleted] = 0 AND f.[Name] = sf.[Name]
                     INNER JOIN [Org].[ProductionLines] pl
                        ON pl.[IsDeleted] = 0
                       AND pl.[FactoryId] = f.[Id]
                       AND pl.[Code] = sl.[Name]
                     WHERE sl.[IsDeleted] = 0) <>
                    (SELECT COUNT(*) FROM [Integration].[EmsLines] WHERE [IsDeleted] = 0))
                    THROW 51002, 'EMS production-line consolidation count mismatch.', 1;

                UPDATE target
                SET target.[EmsSourceRecordId] = source.[Id],
                    target.[CustomerId] = factory.[CustomerId],
                    target.[FactoryId] = factory.[Id],
                    target.[LineId] = NULL,
                    target.[Quality] = CASE
                        WHEN source.[Fault] BETWEEN -32768 AND 32767
                            THEN CONVERT(smallint, source.[Fault])
                        ELSE NULL END,
                    target.[VoltageL1] = source.[V1],
                    target.[VoltageL2] = source.[V2],
                    target.[VoltageL3] = source.[V3],
                    target.[CurrentL1] = source.[I1],
                    target.[CurrentL2] = source.[I2],
                    target.[CurrentL3] = source.[I3],
                    target.[PowerFactorL1] = source.[PF1],
                    target.[PowerFactorL2] = source.[PF2],
                    target.[PowerFactorL3] = source.[PF3],
                    target.[ApparentPowerL1] = source.[Papp1],
                    target.[ApparentPowerL2] = source.[Papp2],
                    target.[ApparentPowerL3] = source.[Papp3],
                    target.[ActivePowerL1] = source.[Pact1],
                    target.[ActivePowerL2] = source.[Pact2],
                    target.[ActivePowerL3] = source.[Pact3],
                    target.[ReactivePowerL1] = source.[Preact1],
                    target.[ReactivePowerL2] = source.[Preact2],
                    target.[ReactivePowerL3] = source.[Preact3],
                    target.[EnergyL1] = source.[Energy1],
                    target.[EnergyL2] = source.[Energy2],
                    target.[EnergyL3] = source.[Energy3],
                    target.[EnergyDiffL1] = source.[DiffEnergy1],
                    target.[EnergyDiffL2] = source.[DiffEnergy2],
                    target.[EnergyDiffL3] = source.[DiffEnergy3],
                    target.[TotalEnergyConsumptionDiff] = source.[TotalEnergyConsumptionDiff],
                    target.[VoltageThdL1] = source.[THDv1],
                    target.[VoltageThdL2] = source.[THDv2],
                    target.[VoltageThdL3] = source.[THDv3],
                    target.[CurrentThdL1] = source.[THDi1],
                    target.[CurrentThdL2] = source.[THDi2],
                    target.[CurrentThdL3] = source.[THDi3],
                    target.[ShiftStartTime] = times.[ShiftStartUtc]
                FROM [Integration].[EmsEnergyReadings] source
                INNER JOIN [MasterData].[Transformers] transformer
                    ON transformer.[IsDeleted] = 0
                   AND transformer.[TransformerName] = source.[SourceId]
                INNER JOIN [Org].[Factories] factory
                    ON factory.[Id] = transformer.[FactoryId]
                   AND factory.[IsDeleted] = 0
                CROSS APPLY (SELECT
                    CONVERT(datetime2, source.[TimeStamp] AT TIME ZONE 'Egypt Standard Time' AT TIME ZONE 'UTC') AS [TimestampUtc],
                    CONVERT(datetime2, source.[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time' AT TIME ZONE 'UTC') AS [ShiftStartUtc]) times
                INNER JOIN [Analytics].[EnergyReadings] target
                    ON target.[TransformerId] = transformer.[Id]
                   AND target.[Timestamp] = times.[TimestampUtc]
                WHERE target.[EmsSourceRecordId] IS NULL;

                INSERT INTO [Analytics].[EnergyReadings]
                    ([EmsSourceRecordId], [CustomerId], [FactoryId], [LineId],
                     [TransformerId], [Quality], [VoltageL1], [VoltageL2], [VoltageL3],
                     [CurrentL1], [CurrentL2], [CurrentL3], [PowerFactorL1],
                     [PowerFactorL2], [PowerFactorL3], [ApparentPowerL1],
                     [ApparentPowerL2], [ApparentPowerL3], [ActivePowerL1],
                     [ActivePowerL2], [ActivePowerL3], [ReactivePowerL1],
                     [ReactivePowerL2], [ReactivePowerL3], [EnergyL1], [EnergyL2],
                     [EnergyL3], [EnergyDiffL1], [EnergyDiffL2], [EnergyDiffL3],
                     [TotalEnergyConsumptionDiff], [VoltageThdL1], [VoltageThdL2],
                     [VoltageThdL3], [CurrentThdL1], [CurrentThdL2], [CurrentThdL3],
                     [Timestamp], [ShiftStartTime])
                SELECT source.[Id], factory.[CustomerId], factory.[Id], NULL,
                       transformer.[Id],
                       CASE WHEN source.[Fault] BETWEEN -32768 AND 32767
                            THEN CONVERT(smallint, source.[Fault]) ELSE NULL END,
                       source.[V1], source.[V2], source.[V3],
                       source.[I1], source.[I2], source.[I3],
                       source.[PF1], source.[PF2], source.[PF3],
                       source.[Papp1], source.[Papp2], source.[Papp3],
                       source.[Pact1], source.[Pact2], source.[Pact3],
                       source.[Preact1], source.[Preact2], source.[Preact3],
                       source.[Energy1], source.[Energy2], source.[Energy3],
                       source.[DiffEnergy1], source.[DiffEnergy2], source.[DiffEnergy3],
                       source.[TotalEnergyConsumptionDiff],
                       source.[THDv1], source.[THDv2], source.[THDv3],
                       source.[THDi1], source.[THDi2], source.[THDi3],
                       times.[TimestampUtc], times.[ShiftStartUtc]
                FROM [Integration].[EmsEnergyReadings] source
                INNER JOIN [MasterData].[Transformers] transformer
                    ON transformer.[IsDeleted] = 0
                   AND transformer.[TransformerName] = source.[SourceId]
                INNER JOIN [Org].[Factories] factory
                    ON factory.[Id] = transformer.[FactoryId]
                   AND factory.[IsDeleted] = 0
                CROSS APPLY (SELECT
                    CONVERT(datetime2, source.[TimeStamp] AT TIME ZONE 'Egypt Standard Time' AT TIME ZONE 'UTC') AS [TimestampUtc],
                    CONVERT(datetime2, source.[ShiftStartTime] AT TIME ZONE 'Egypt Standard Time' AT TIME ZONE 'UTC') AS [ShiftStartUtc]) times
                WHERE NOT EXISTS (
                    SELECT 1 FROM [Analytics].[EnergyReadings] target
                    WHERE target.[EmsSourceRecordId] = source.[Id]
                       OR (target.[TransformerId] = transformer.[Id]
                           AND target.[Timestamp] = times.[TimestampUtc]));

                IF ((SELECT COUNT(*) FROM [Analytics].[EnergyReadings]
                     WHERE [EmsSourceRecordId] IN
                         (SELECT [Id] FROM [Integration].[EmsEnergyReadings])) <>
                    (SELECT COUNT(*) FROM [Integration].[EmsEnergyReadings]))
                    THROW 51003, 'EMS Energy consolidation count mismatch.', 1;
                """);

            migrationBuilder.DropTable(
                name: "EmsEnergyReadings",
                schema: "Integration");

            migrationBuilder.DropTable(
                name: "EmsLineTransformers",
                schema: "Integration");

            migrationBuilder.DropTable(
                name: "EmsLines",
                schema: "Integration");

            migrationBuilder.DropTable(
                name: "EmsZones",
                schema: "Integration");

            migrationBuilder.DropTable(
                name: "EmsTransformers",
                schema: "Integration");

            migrationBuilder.DropTable(
                name: "EmsFactories",
                schema: "Integration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionLines_EnergyZones_EnergyZoneId",
                schema: "Org",
                table: "ProductionLines");

            migrationBuilder.DropTable(
                name: "EnergyZones",
                schema: "Org");

            migrationBuilder.DropIndex(
                name: "IX_ProductionLines_EnergyZoneId",
                schema: "Org",
                table: "ProductionLines");

            migrationBuilder.DropIndex(
                name: "IX_EnergyReadings_EmsSourceRecordId",
                schema: "Analytics",
                table: "EnergyReadings");

            migrationBuilder.DropColumn(
                name: "EnergyRatioFromParent",
                schema: "Org",
                table: "ProductionLines");

            migrationBuilder.DropColumn(
                name: "EnergyZoneId",
                schema: "Org",
                table: "ProductionLines");

            migrationBuilder.DropColumn(
                name: "EmsSourceRecordId",
                schema: "Analytics",
                table: "EnergyReadings");

            migrationBuilder.DropColumn(
                name: "EnergyDiffL1",
                schema: "Analytics",
                table: "EnergyReadings");

            migrationBuilder.DropColumn(
                name: "EnergyDiffL2",
                schema: "Analytics",
                table: "EnergyReadings");

            migrationBuilder.DropColumn(
                name: "EnergyDiffL3",
                schema: "Analytics",
                table: "EnergyReadings");

            migrationBuilder.DropColumn(
                name: "ShiftStartTime",
                schema: "Analytics",
                table: "EnergyReadings");

            migrationBuilder.DropColumn(
                name: "TotalEnergyConsumptionDiff",
                schema: "Analytics",
                table: "EnergyReadings");

            migrationBuilder.CreateTable(
                name: "EmsEnergyReadings",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    AvgCurrent = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgPact = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgPapp = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgPowerFactor = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgPreact = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgTHDi = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgTHDv = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgVoltage = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DiffEnergy1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DiffEnergy2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DiffEnergy3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Energy1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Energy2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Energy3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Fault = table.Column<int>(type: "int", nullable: true),
                    I1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    I2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    I3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PF1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PF2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PF3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Pact1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Pact2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Pact3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Papp1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Papp2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Papp3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Preact1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Preact2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Preact3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ShiftStartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SumEnergy = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDi1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDi2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDi3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDv1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDv2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDv3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalEnergyConsumptionDiff = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    V1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    V2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    V3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmsEnergyReadings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmsFactories",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmsFactories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmsTransformers",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FactoryId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TransformerName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmsTransformers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmsTransformers_EmsFactories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "Integration",
                        principalTable: "EmsFactories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmsZones",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FactoryId = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RatioFromParent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TransformerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmsZones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmsZones_EmsFactories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "Integration",
                        principalTable: "EmsFactories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmsZones_EmsTransformers_TransformerId",
                        column: x => x.TransformerId,
                        principalSchema: "Integration",
                        principalTable: "EmsTransformers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmsLines",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FactoryId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    RatioFromParent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ViewName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZoneId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmsLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmsLines_EmsFactories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "Integration",
                        principalTable: "EmsFactories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmsLines_EmsZones_ZoneId",
                        column: x => x.ZoneId,
                        principalSchema: "Integration",
                        principalTable: "EmsZones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmsLineTransformers",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LineId = table.Column<int>(type: "int", nullable: false),
                    TransformerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmsLineTransformers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmsLineTransformers_EmsLines_LineId",
                        column: x => x.LineId,
                        principalSchema: "Integration",
                        principalTable: "EmsLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmsLineTransformers_EmsTransformers_TransformerId",
                        column: x => x.TransformerId,
                        principalSchema: "Integration",
                        principalTable: "EmsTransformers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmsEnergyReadings_ShiftStartTime",
                schema: "Integration",
                table: "EmsEnergyReadings",
                column: "ShiftStartTime");

            migrationBuilder.CreateIndex(
                name: "IX_EmsEnergyReadings_TimeStamp",
                schema: "Integration",
                table: "EmsEnergyReadings",
                column: "TimeStamp");

            migrationBuilder.CreateIndex(
                name: "IX_EmsLines_FactoryId",
                schema: "Integration",
                table: "EmsLines",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmsLines_ZoneId",
                schema: "Integration",
                table: "EmsLines",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_EmsLineTransformers_LineId",
                schema: "Integration",
                table: "EmsLineTransformers",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_EmsLineTransformers_TransformerId",
                schema: "Integration",
                table: "EmsLineTransformers",
                column: "TransformerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmsTransformers_FactoryId",
                schema: "Integration",
                table: "EmsTransformers",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmsZones_FactoryId",
                schema: "Integration",
                table: "EmsZones",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_EmsZones_TransformerId",
                schema: "Integration",
                table: "EmsZones",
                column: "TransformerId");
        }
    }
}
