using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmsEnergyLiveMirror : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmsEnergyReadings",
                schema: "Integration",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    V1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    V2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    V3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    I1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    I2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    I3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PF1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PF2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PF3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Papp1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Papp2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Papp3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Pact1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Pact2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Pact3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Preact1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Preact2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Preact3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Energy1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Energy2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Energy3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DiffEnergy1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DiffEnergy2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DiffEnergy3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    THDv1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDv2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDv3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDi1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDi2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    THDi3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Fault = table.Column<int>(type: "int", nullable: true),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShiftStartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvgVoltage = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgCurrent = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgPowerFactor = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgPact = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgPreact = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgPapp = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    SumEnergy = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgTHDi = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AvgTHDv = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TotalEnergyConsumptionDiff = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                    TransformerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FactoryId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FactoryId = table.Column<int>(type: "int", nullable: false),
                    RatioFromParent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TransformerId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FactoryId = table.Column<int>(type: "int", nullable: true),
                    Area = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ViewName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RatioFromParent = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ZoneId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                    LineId = table.Column<int>(type: "int", nullable: false),
                    TransformerId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
