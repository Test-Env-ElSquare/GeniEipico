using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEipicoSapRealtimeIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Integration");

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                schema: "Production",
                table: "BatchMaterials",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OperationCode",
                schema: "Production",
                table: "BatchMaterials",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceRecordId",
                schema: "Production",
                table: "BatchMaterials",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uom",
                schema: "Production",
                table: "BatchMaterials",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "PlannedStart",
                schema: "Production",
                table: "Batches",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<int>(
                name: "PlannedQty",
                schema: "Production",
                table: "Batches",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "JobOrderId",
                schema: "Production",
                table: "Batches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                schema: "Production",
                table: "Batches",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceRecordId",
                schema: "Production",
                table: "Batches",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                schema: "Production",
                table: "Batches",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StorageLocation",
                schema: "Production",
                table: "Batches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseCode",
                schema: "Production",
                table: "Batches",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnergyHarmonicReadings",
                schema: "Analytics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransformerId = table.Column<int>(type: "int", nullable: false),
                    SourceRecordId = table.Column<long>(type: "bigint", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HarmonicI31 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI32 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI33 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV31 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV32 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV33 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI51 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI52 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI53 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV51 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV52 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV53 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI71 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI72 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI73 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV71 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV72 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV73 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI91 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI92 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI93 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV91 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV92 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV93 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI111 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI112 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI113 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV111 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV112 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV113 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI131 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI132 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicI133 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV131 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV132 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    HarmonicV133 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnergyHarmonicReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnergyHarmonicReadings_Transformers_TransformerId",
                        column: x => x.TransformerId,
                        principalSchema: "MasterData",
                        principalTable: "Transformers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialSplits",
                schema: "Production",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchMaterialId = table.Column<int>(type: "int", nullable: false),
                    SplitUid = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SplitAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceRecordId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSplits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSplits_BatchMaterials_BatchMaterialId",
                        column: x => x.BatchMaterialId,
                        principalSchema: "Production",
                        principalTable: "BatchMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SyncCheckpoints",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StreamName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastSourceId = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncErrors",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StreamName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceRecordId = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    FirstOccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastOccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncErrors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeighingEvents",
                schema: "Production",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    BatchMaterialId = table.Column<int>(type: "int", nullable: false),
                    ScaleId = table.Column<int>(type: "int", nullable: true),
                    MaterialUid = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Dispatched = table.Column<bool>(type: "bit", nullable: false),
                    WeighingSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceRecordId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeighingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeighingEvents_BatchMaterials_BatchMaterialId",
                        column: x => x.BatchMaterialId,
                        principalSchema: "Production",
                        principalTable: "BatchMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeighingEvents_Batches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "Production",
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WeighingEvents_Scales_ScaleId",
                        column: x => x.ScaleId,
                        principalSchema: "RealTime",
                        principalTable: "Scales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MachineMaterialLoads",
                schema: "Production",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    BatchMaterialId = table.Column<int>(type: "int", nullable: false),
                    MaterialSplitId = table.Column<int>(type: "int", nullable: true),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    MaterialUid = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LoadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShiftStartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinishingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsPending = table.Column<bool>(type: "bit", nullable: true),
                    Count = table.Column<int>(type: "int", nullable: true),
                    SourceRecordId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineMaterialLoads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MachineMaterialLoads_BatchMaterials_BatchMaterialId",
                        column: x => x.BatchMaterialId,
                        principalSchema: "Production",
                        principalTable: "BatchMaterials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineMaterialLoads_Batches_BatchId",
                        column: x => x.BatchId,
                        principalSchema: "Production",
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineMaterialLoads_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "Scada",
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MachineMaterialLoads_MaterialSplits_MaterialSplitId",
                        column: x => x.MaterialSplitId,
                        principalSchema: "Production",
                        principalTable: "MaterialSplits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BatchMaterials_SourceRecordId",
                schema: "Production",
                table: "BatchMaterials",
                column: "SourceRecordId",
                unique: true,
                filter: "[SourceRecordId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_SourceType_SourceRecordId",
                schema: "Production",
                table: "Batches",
                columns: new[] { "SourceType", "SourceRecordId" },
                unique: true,
                filter: "[SourceRecordId] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyHarmonicReadings_SourceRecordId",
                schema: "Analytics",
                table: "EnergyHarmonicReadings",
                column: "SourceRecordId",
                unique: true,
                filter: "[SourceRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EnergyHarmonicReadings_TransformerId_Timestamp",
                schema: "Analytics",
                table: "EnergyHarmonicReadings",
                columns: new[] { "TransformerId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineMaterialLoads_BatchId_MachineId_LoadedAt",
                schema: "Production",
                table: "MachineMaterialLoads",
                columns: new[] { "BatchId", "MachineId", "LoadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineMaterialLoads_BatchMaterialId",
                schema: "Production",
                table: "MachineMaterialLoads",
                column: "BatchMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineMaterialLoads_MachineId",
                schema: "Production",
                table: "MachineMaterialLoads",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineMaterialLoads_MaterialSplitId",
                schema: "Production",
                table: "MachineMaterialLoads",
                column: "MaterialSplitId");

            migrationBuilder.CreateIndex(
                name: "IX_MachineMaterialLoads_SourceRecordId",
                schema: "Production",
                table: "MachineMaterialLoads",
                column: "SourceRecordId",
                unique: true,
                filter: "[SourceRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSplits_BatchMaterialId",
                schema: "Production",
                table: "MaterialSplits",
                column: "BatchMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSplits_SourceRecordId",
                schema: "Production",
                table: "MaterialSplits",
                column: "SourceRecordId",
                unique: true,
                filter: "[SourceRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSplits_SplitUid",
                schema: "Production",
                table: "MaterialSplits",
                column: "SplitUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncCheckpoints_SourceSystem_StreamName",
                schema: "Integration",
                table: "SyncCheckpoints",
                columns: new[] { "SourceSystem", "StreamName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncErrors_ResolvedAt",
                schema: "Integration",
                table: "SyncErrors",
                column: "ResolvedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SyncErrors_SourceSystem_StreamName_SourceRecordId",
                schema: "Integration",
                table: "SyncErrors",
                columns: new[] { "SourceSystem", "StreamName", "SourceRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_WeighingEvents_BatchId_RecordedAt",
                schema: "Production",
                table: "WeighingEvents",
                columns: new[] { "BatchId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WeighingEvents_BatchMaterialId",
                schema: "Production",
                table: "WeighingEvents",
                column: "BatchMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingEvents_ScaleId",
                schema: "Production",
                table: "WeighingEvents",
                column: "ScaleId");

            migrationBuilder.CreateIndex(
                name: "IX_WeighingEvents_WeighingSource_SourceRecordId",
                schema: "Production",
                table: "WeighingEvents",
                columns: new[] { "WeighingSource", "SourceRecordId" },
                unique: true,
                filter: "[SourceRecordId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnergyHarmonicReadings",
                schema: "Analytics");

            migrationBuilder.DropTable(
                name: "MachineMaterialLoads",
                schema: "Production");

            migrationBuilder.DropTable(
                name: "SyncCheckpoints",
                schema: "Integration");

            migrationBuilder.DropTable(
                name: "SyncErrors",
                schema: "Integration");

            migrationBuilder.DropTable(
                name: "WeighingEvents",
                schema: "Production");

            migrationBuilder.DropTable(
                name: "MaterialSplits",
                schema: "Production");

            migrationBuilder.DropIndex(
                name: "IX_BatchMaterials_SourceRecordId",
                schema: "Production",
                table: "BatchMaterials");

            migrationBuilder.DropIndex(
                name: "IX_Batches_SourceType_SourceRecordId",
                schema: "Production",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                schema: "Production",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "OperationCode",
                schema: "Production",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "SourceRecordId",
                schema: "Production",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "Uom",
                schema: "Production",
                table: "BatchMaterials");

            migrationBuilder.DropColumn(
                name: "JobOrderId",
                schema: "Production",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                schema: "Production",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "SourceRecordId",
                schema: "Production",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "SourceType",
                schema: "Production",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "StorageLocation",
                schema: "Production",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "WarehouseCode",
                schema: "Production",
                table: "Batches");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PlannedStart",
                schema: "Production",
                table: "Batches",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PlannedQty",
                schema: "Production",
                table: "Batches",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
