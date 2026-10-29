using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorRealtimeBusinessTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "RealTime");

            // These three tables contain volatile readings. Their old rows cannot be
            // converted safely because the external source identifier was not stored.
            migrationBuilder.DropTable(
                name: "TelemetryTags",
                schema: "IoT");

            // EnergyReadings was introduced by an earlier telemetry experiment and is
            // not part of the current EF model. Preserve the table and move its FK to
            // the new raw-telemetry table when it exists in an already deployed DB.
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NOT NULL
                   AND EXISTS
                   (
                       SELECT 1
                       FROM sys.foreign_keys
                       WHERE [name] = N'FK_EnergyReadings_TelemetryRaw_TelemetryRawId'
                         AND [parent_object_id] = OBJECT_ID(N'[Analytics].[EnergyReadings]')
                   )
                BEGIN
                    ALTER TABLE [Analytics].[EnergyReadings]
                    DROP CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId];
                END;
                """);

            migrationBuilder.DropTable(
                name: "TelemetryRaw",
                schema: "IoT");

            migrationBuilder.DropTable(
                name: "MachineStates",
                schema: "Scada");

            // Scales and silos keep their identifiers and data; only their schema changes.
            migrationBuilder.RenameTable(
                name: "Scales",
                schema: "Scada",
                newName: "Scales",
                newSchema: "RealTime");

            migrationBuilder.RenameTable(
                name: "Silos",
                schema: "Scada",
                newName: "Silos",
                newSchema: "RealTime");

            migrationBuilder.CreateTable(
                name: "Production",
                schema: "RealTime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Speed = table.Column<int>(type: "int", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    CountDiff = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    LineId = table.Column<int>(type: "int", nullable: false),
                    FactoryId = table.Column<int>(type: "int", nullable: false),
                    Temp = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: true),
                    Qreject = table.Column<int>(type: "int", nullable: false),
                    StateReason = table.Column<int>(type: "int", nullable: false),
                    SKU = table.Column<int>(type: "int", nullable: false),
                    Fault = table.Column<int>(type: "int", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Production", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Production_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "Org",
                        principalTable: "Factories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Production_ProductionLines_LineId",
                        column: x => x.LineId,
                        principalSchema: "Org",
                        principalTable: "ProductionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryRaw",
                schema: "RealTime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourcePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryRaw", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryTags",
                schema: "RealTime",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TagName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValueNum = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ValueStr = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryTags", x => x.Id);
                });

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[Analytics].[EnergyReadings]', N'TelemetryRawId') IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM sys.foreign_keys
                       WHERE [name] = N'FK_EnergyReadings_TelemetryRaw_TelemetryRawId'
                         AND [parent_object_id] = OBJECT_ID(N'[Analytics].[EnergyReadings]')
                   )
                BEGIN
                    ALTER TABLE [Analytics].[EnergyReadings] WITH CHECK
                    ADD CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId]
                    FOREIGN KEY ([TelemetryRawId])
                    REFERENCES [RealTime].[TelemetryRaw] ([Id]);

                    ALTER TABLE [Analytics].[EnergyReadings]
                    CHECK CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId];
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Production_FactoryId_LineId",
                schema: "RealTime",
                table: "Production",
                columns: new[] { "FactoryId", "LineId" });

            migrationBuilder.CreateIndex(
                name: "IX_Production_FactoryId_MachineId",
                schema: "RealTime",
                table: "Production",
                columns: new[] { "FactoryId", "MachineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Production_LineId",
                schema: "RealTime",
                table: "Production",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryRaw_ReceivedAt",
                schema: "RealTime",
                table: "TelemetryRaw",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryRaw_SourcePath_ReceivedAt",
                schema: "RealTime",
                table: "TelemetryRaw",
                columns: new[] { "SourcePath", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryTags_DeviceId_TagName_RecordedAt",
                schema: "RealTime",
                table: "TelemetryTags",
                columns: new[] { "DeviceId", "TagName", "RecordedAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NOT NULL
                   AND EXISTS
                   (
                       SELECT 1
                       FROM sys.foreign_keys
                       WHERE [name] = N'FK_EnergyReadings_TelemetryRaw_TelemetryRawId'
                         AND [parent_object_id] = OBJECT_ID(N'[Analytics].[EnergyReadings]')
                   )
                BEGIN
                    ALTER TABLE [Analytics].[EnergyReadings]
                    DROP CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId];
                END;
                """);

            migrationBuilder.DropTable(
                name: "Production",
                schema: "RealTime");

            migrationBuilder.DropTable(
                name: "TelemetryTags",
                schema: "RealTime");

            migrationBuilder.DropTable(
                name: "TelemetryRaw",
                schema: "RealTime");

            migrationBuilder.RenameTable(
                name: "Scales",
                schema: "RealTime",
                newName: "Scales",
                newSchema: "Scada");

            migrationBuilder.RenameTable(
                name: "Silos",
                schema: "RealTime",
                newName: "Silos",
                newSchema: "Scada");

            migrationBuilder.CreateTable(
                name: "MachineStates",
                schema: "Scada",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SpeedRpm = table.Column<int>(type: "int", nullable: true),
                    TempCelsius = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    VibrationMms = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    EnergyKwh = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: true),
                    ActiveBatchId = table.Column<int>(type: "int", nullable: true),
                    StateStartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TelemetryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineStates_Batches_ActiveBatchId",
                        column: x => x.ActiveBatchId,
                        principalSchema: "Production",
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineStates_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "Scada",
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryRaw",
                schema: "IoT",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Protocol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Quality = table.Column<short>(type: "smallint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryRaw", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryRaw_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "IoT",
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TelemetryTags",
                schema: "IoT",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    TagName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ValueNum = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    ValueStr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelemetryTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TelemetryTags_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalSchema: "IoT",
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[Analytics].[EnergyReadings]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[Analytics].[EnergyReadings]', N'TelemetryRawId') IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM sys.foreign_keys
                       WHERE [name] = N'FK_EnergyReadings_TelemetryRaw_TelemetryRawId'
                         AND [parent_object_id] = OBJECT_ID(N'[Analytics].[EnergyReadings]')
                   )
                BEGIN
                    ALTER TABLE [Analytics].[EnergyReadings] WITH CHECK
                    ADD CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId]
                    FOREIGN KEY ([TelemetryRawId])
                    REFERENCES [IoT].[TelemetryRaw] ([Id]);

                    ALTER TABLE [Analytics].[EnergyReadings]
                    CHECK CONSTRAINT [FK_EnergyReadings_TelemetryRaw_TelemetryRawId];
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MachineStates_ActiveBatchId",
                schema: "Scada",
                table: "MachineStates",
                column: "ActiveBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineStates_MachineId",
                schema: "Scada",
                table: "MachineStates",
                column: "MachineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryRaw_DeviceId_RecordedAt",
                schema: "IoT",
                table: "TelemetryRaw",
                columns: new[] { "DeviceId", "RecordedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelemetryTags_DeviceId_TagName_RecordedAt",
                schema: "IoT",
                table: "TelemetryTags",
                columns: new[] { "DeviceId", "TagName", "RecordedAt" },
                unique: true);
        }
    }
}
