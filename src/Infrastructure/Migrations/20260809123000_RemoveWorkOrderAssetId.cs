using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(MesDbContext))]
[Migration("20260809123000_RemoveWorkOrderAssetId")]
public partial class RemoveWorkOrderAssetId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE workOrder
            SET MachineId = asset.MachineId
            FROM Maintenance.WorkOrders AS workOrder
            INNER JOIN Asset.Assets AS asset ON asset.Id = workOrder.AssetId
            WHERE workOrder.MachineId IS NULL
              AND asset.MachineId IS NOT NULL;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_WorkOrders_Assets_AssetId",
            schema: "Maintenance",
            table: "WorkOrders");

        migrationBuilder.DropIndex(
            name: "IX_WorkOrders_AssetId",
            schema: "Maintenance",
            table: "WorkOrders");

        migrationBuilder.DropColumn(
            name: "AssetId",
            schema: "Maintenance",
            table: "WorkOrders");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AssetId",
            schema: "Maintenance",
            table: "WorkOrders",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_WorkOrders_AssetId",
            schema: "Maintenance",
            table: "WorkOrders",
            column: "AssetId");

        migrationBuilder.AddForeignKey(
            name: "FK_WorkOrders_Assets_AssetId",
            schema: "Maintenance",
            table: "WorkOrders",
            column: "AssetId",
            principalSchema: "Asset",
            principalTable: "Assets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }
}
