using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerIdToProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                schema: "RealTime",
                table: "Production",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE production
                SET production.[CustomerId] = factory.[CustomerId]
                FROM [RealTime].[Production] AS production
                INNER JOIN [Org].[Factories] AS factory
                    ON factory.[Id] = production.[FactoryId];

                IF EXISTS (
                    SELECT 1
                    FROM [RealTime].[Production]
                    WHERE [CustomerId] IS NULL
                )
                    THROW 51001, 'Could not resolve CustomerId for every Production row.', 1;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CustomerId",
                schema: "RealTime",
                table: "Production",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Production_CustomerId",
                schema: "RealTime",
                table: "Production",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Production_Customers_CustomerId",
                schema: "RealTime",
                table: "Production",
                column: "CustomerId",
                principalSchema: "Org",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Production_Customers_CustomerId",
                schema: "RealTime",
                table: "Production");

            migrationBuilder.DropIndex(
                name: "IX_Production_CustomerId",
                schema: "RealTime",
                table: "Production");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "RealTime",
                table: "Production");
        }
    }
}
