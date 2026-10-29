using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOeeSnapshotScopeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                schema: "Analytics",
                table: "OeeSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FactoryId",
                schema: "Analytics",
                table: "OeeSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineId",
                schema: "Analytics",
                table: "OeeSnapshots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MachineName",
                schema: "Analytics",
                table: "OeeSnapshots",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "Analytics",
                table: "OeeSnapshots");

            migrationBuilder.DropColumn(
                name: "FactoryId",
                schema: "Analytics",
                table: "OeeSnapshots");

            migrationBuilder.DropColumn(
                name: "LineId",
                schema: "Analytics",
                table: "OeeSnapshots");

            migrationBuilder.DropColumn(
                name: "MachineName",
                schema: "Analytics",
                table: "OeeSnapshots");
        }
    }
}
