using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(MesDbContext))]
[Migration("20261028090000_AddProductionLineTransformerLinks")]
public partial class AddProductionLineTransformerLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProductionLineTransformers",
            schema: "Org",
            columns: table => new
            {
                ProductionLineId = table.Column<int>(type: "int", nullable: false),
                TransformerId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_ProductionLineTransformers",
                    x => new { x.ProductionLineId, x.TransformerId });
                table.ForeignKey(
                    name: "FK_ProductionLineTransformers_ProductionLines",
                    column: x => x.ProductionLineId,
                    principalSchema: "Org",
                    principalTable: "ProductionLines",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ProductionLineTransformers_Transformers",
                    column: x => x.TransformerId,
                    principalSchema: "MasterData",
                    principalTable: "Transformers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProductionLineTransformers_TransformerId",
            schema: "Org",
            table: "ProductionLineTransformers",
            column: "TransformerId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProductionLineTransformers",
            schema: "Org");
    }
}
