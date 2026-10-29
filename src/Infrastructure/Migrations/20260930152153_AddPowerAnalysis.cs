using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPowerAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Calculated");

            migrationBuilder.CreateTable(
                name: "PowerAnalysis",
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
                    table.PrimaryKey("PK_PowerAnalysis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PowerAnalysis_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "Org",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PowerAnalysis_Factories_FactoryId",
                        column: x => x.FactoryId,
                        principalSchema: "Org",
                        principalTable: "Factories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PowerAnalysis_ProductionLines_LineId",
                        column: x => x.LineId,
                        principalSchema: "Org",
                        principalTable: "ProductionLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PowerAnalysis_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalSchema: "Planning",
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PowerAnalysis_Transformers_TransformerId",
                        column: x => x.TransformerId,
                        principalSchema: "MasterData",
                        principalTable: "Transformers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysis_CustomerId_FactoryId_LineId_ShiftStartTime",
                schema: "Calculated",
                table: "PowerAnalysis",
                columns: new[] { "CustomerId", "FactoryId", "LineId", "ShiftStartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysis_FactoryId",
                schema: "Calculated",
                table: "PowerAnalysis",
                column: "FactoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysis_LineId",
                schema: "Calculated",
                table: "PowerAnalysis",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysis_ShiftId",
                schema: "Calculated",
                table: "PowerAnalysis",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_PowerAnalysis_TransformerId_ShiftId_ShiftStartTime",
                schema: "Calculated",
                table: "PowerAnalysis",
                columns: new[] { "TransformerId", "ShiftId", "ShiftStartTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PowerAnalysis",
                schema: "Calculated");
        }
    }
}
