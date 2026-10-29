using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineLoadLineForSignage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LineId",
                schema: "Production",
                table: "MachineMaterialLoads",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MachineMaterialLoads_LineId",
                schema: "Production",
                table: "MachineMaterialLoads",
                column: "LineId");

            migrationBuilder.AddForeignKey(
                name: "FK_MachineMaterialLoads_ProductionLines_LineId",
                schema: "Production",
                table: "MachineMaterialLoads",
                column: "LineId",
                principalSchema: "Org",
                principalTable: "ProductionLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MachineMaterialLoads_ProductionLines_LineId",
                schema: "Production",
                table: "MachineMaterialLoads");

            migrationBuilder.DropIndex(
                name: "IX_MachineMaterialLoads_LineId",
                schema: "Production",
                table: "MachineMaterialLoads");

            migrationBuilder.DropColumn(
                name: "LineId",
                schema: "Production",
                table: "MachineMaterialLoads");
        }
    }
}
