using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEipicoEnergySourceSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SourceRecordId",
                schema: "Analytics",
                table: "EnergyReadings",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnergyReadings_SourceRecordId",
                schema: "Analytics",
                table: "EnergyReadings",
                column: "SourceRecordId",
                unique: true,
                filter: "[SourceRecordId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EnergyReadings_SourceRecordId",
                schema: "Analytics",
                table: "EnergyReadings");

            migrationBuilder.DropColumn(
                name: "SourceRecordId",
                schema: "Analytics",
                table: "EnergyReadings");
        }
    }
}
