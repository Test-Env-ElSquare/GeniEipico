using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(MesDbContext))]
[Migration("20261021120000_WidenBatchMaterialPlannedQuantity")]
public partial class WidenBatchMaterialPlannedQuantity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "PlannedQty",
            schema: "Production",
            table: "BatchMaterials",
            type: "decimal(18,4)",
            precision: 18,
            scale: 4,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "decimal(12,4)",
            oldPrecision: 12,
            oldScale: 4);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "PlannedQty",
            schema: "Production",
            table: "BatchMaterials",
            type: "decimal(12,4)",
            precision: 12,
            scale: 4,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "decimal(18,4)",
            oldPrecision: 18,
            oldScale: 4);
    }
}
