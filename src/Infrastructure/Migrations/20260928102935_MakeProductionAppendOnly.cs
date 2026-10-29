using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeProductionAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Production_FactoryId_MachineId",
                schema: "RealTime",
                table: "Production");

            migrationBuilder.CreateIndex(
                name: "IX_Production_FactoryId_MachineId_TimeStamp",
                schema: "RealTime",
                table: "Production",
                columns: new[] { "FactoryId", "MachineId", "TimeStamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Production_FactoryId_MachineId_TimeStamp",
                schema: "RealTime",
                table: "Production");

            migrationBuilder.CreateIndex(
                name: "IX_Production_FactoryId_MachineId",
                schema: "RealTime",
                table: "Production",
                columns: new[] { "FactoryId", "MachineId" },
                unique: true);
        }
    }
}
