using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerAnalysisPerHour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PowerAnalysisPerHour",
                schema: "Calculated",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransformerId = table.Column<int>(type: "int", nullable: false),
                    TransformerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    FactoryId = table.Column<int>(type: "int", nullable: false),
                    LineId = table.Column<int>(type: "int", nullable: true),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    ShiftName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ShiftStartTime = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ShiftEndTime = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    HourStartTime = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    HourEndTime = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    TotalEnergyConsumption = table.Column<decimal>(type: "decimal(38,6)", precision: 38, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: false),
                    PowerFactor = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false),
                    Voltage = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false),
                    Current = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false),
                    Hermonics = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false),
                    AvgTHDv = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false),
                    AvgTHDi = table.Column<decimal>(type: "decimal(28,6)", precision: 28, scale: 6, nullable: false),
                    LastReadingAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ReadingCount = table.Column<long>(type: "bigint", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PowerAnalysisPerHour", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PowerAnalysisPerHour_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "Org",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PowerAnalysisPerHour_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "Org",
                        principalTable: "Factories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PowerAnalysisPerHour_ProductionLines_LineId",
                        column: x => x.LineId,
                        principalSchema: "Org",
                        principalTable: "ProductionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PowerAnalysisPerHour_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "Planning",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PowerAnalysisPerHour_Transformers_TransformerId",
                        column: x => x.TransformerId,
                        principalSchema: "MasterData",
                        principalTable: "Transformers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysisPerHour_CustomerId_FactoryId_LineId_HourStartTime",
                schema: "Calculated",
                table: "PowerAnalysisPerHour",
                columns: new[] { "CustomerId", "FactoryId", "LineId", "HourStartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysisPerHour_FactoryId",
                schema: "Calculated",
                table: "PowerAnalysisPerHour",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysisPerHour_LineId",
                schema: "Calculated",
                table: "PowerAnalysisPerHour",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysisPerHour_ShiftId",
                schema: "Calculated",
                table: "PowerAnalysisPerHour",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysisPerHour_TransformerId_ShiftId_HourStartTime",
                schema: "Calculated",
                table: "PowerAnalysisPerHour",
                columns: new[] { "TransformerId", "ShiftId", "HourStartTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PowerAnalysisPerHour",
                schema: "Calculated");
        }
    }
}
