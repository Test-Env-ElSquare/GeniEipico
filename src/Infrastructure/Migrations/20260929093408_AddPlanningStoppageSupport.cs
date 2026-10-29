using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningStoppageSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "BatchId",
                schema: "Planning",
                table: "ProductionSchedules",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "Planning",
                table: "ProductionSchedules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Never replace a standalone stoppage's BatchId with the invalid FK value 0.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [Planning].[ProductionSchedules] WHERE [BatchId] IS NULL) THROW 51000, 'Cannot downgrade while standalone stoppages exist. Archive/export and resolve these rows explicitly first.', 1;");
            migrationBuilder.DropColumn(
                name: "Title",
                schema: "Planning",
                table: "ProductionSchedules");

            migrationBuilder.AlterColumn<int>(
                name: "BatchId",
                schema: "Planning",
                table: "ProductionSchedules",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
