using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOeeSnapshotsPerHour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OeeSnapshotsPerHour",
                schema: "Analytics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MachineId = table.Column<int>(type: "int", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    FactoryId = table.Column<int>(type: "int", nullable: true),
                    LineId = table.Column<int>(type: "int", nullable: true),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    ShiftDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailabilityPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    PerformancePct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    QualityPct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    OeePct = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AverageSpeedRpm = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LastState = table.Column<int>(type: "int", nullable: true),
                    TotalOutput = table.Column<int>(type: "int", nullable: false),
                    RejectedUnits = table.Column<int>(type: "int", nullable: false),
                    DowntimeMin = table.Column<int>(type: "int", nullable: true),
                    EnergyKwh = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OeeSnapshotsPerHour", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OeeSnapshotsPerHour_Machines_MachineId",
                        column: x => x.MachineId,
                        principalSchema: "Scada",
                        principalTable: "Machines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OeeSnapshotsPerHour_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "Planning",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OeeSnapshotsPerHour_MachineId_ShiftDate",
                schema: "Analytics",
                table: "OeeSnapshotsPerHour",
                columns: new[] { "MachineId", "ShiftDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OeeSnapshotsPerHour_MachineId_ShiftId_ShiftDate",
                schema: "Analytics",
                table: "OeeSnapshotsPerHour",
                columns: new[] { "MachineId", "ShiftId", "ShiftDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OeeSnapshotsPerHour_ShiftId",
                schema: "Analytics",
                table: "OeeSnapshotsPerHour",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OeeSnapshotsPerHour",
                schema: "Analytics");
        }
    }
}
