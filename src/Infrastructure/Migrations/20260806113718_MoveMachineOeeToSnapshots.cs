using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(MesDbContext))]
[Migration("20260806113718_MoveMachineOeeToSnapshots")]
public partial class MoveMachineOeeToSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "OeePct",
            schema: "Scada",
            table: "MachineStates");

        migrationBuilder.AddColumn<decimal>(
            name: "AverageSpeedRpm",
            schema: "Analytics",
            table: "OeeSnapshots",
            type: "decimal(10,2)",
            precision: 10,
            scale: 2,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AverageSpeedRpm",
            schema: "Analytics",
            table: "OeeSnapshots");

        migrationBuilder.AddColumn<decimal>(
            name: "OeePct",
            schema: "Scada",
            table: "MachineStates",
            type: "decimal(5,2)",
            precision: 5,
            scale: 2,
            nullable: true);
    }
}
