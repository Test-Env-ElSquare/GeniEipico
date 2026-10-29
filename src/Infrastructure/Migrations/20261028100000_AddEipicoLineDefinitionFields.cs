using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(MesDbContext))]
[Migration("20261028100000_AddEipicoLineDefinitionFields")]
public partial class AddEipicoLineDefinitionFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AliasName",
            schema: "Org",
            table: "ProductionLines",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Area",
            schema: "Org",
            table: "ProductionLines",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Number",
            schema: "Org",
            table: "ProductionLines",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ViewName",
            schema: "Org",
            table: "ProductionLines",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "AliasName", schema: "Org", table: "ProductionLines");
        migrationBuilder.DropColumn(name: "Area", schema: "Org", table: "ProductionLines");
        migrationBuilder.DropColumn(name: "Number", schema: "Org", table: "ProductionLines");
        migrationBuilder.DropColumn(name: "ViewName", schema: "Org", table: "ProductionLines");
    }
}
