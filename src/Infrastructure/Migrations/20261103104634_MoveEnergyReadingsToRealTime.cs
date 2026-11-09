using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveEnergyReadingsToRealTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "EnergyReadings",
                schema: "Analytics",
                newName: "EnergyReadings",
                newSchema: "RealTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "EnergyReadings",
                schema: "RealTime",
                newName: "EnergyReadings",
                newSchema: "Analytics");
        }
    }
}
