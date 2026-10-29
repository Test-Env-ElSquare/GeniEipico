using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(MesDbContext))]
[Migration("20260803090000_AddAlarmRuleRange")]
public partial class AddAlarmRuleRange : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Operator",
            schema: "Alarm",
            table: "AlarmRules",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(10)",
            oldMaxLength: 10,
            oldNullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MaximumValue",
            schema: "Alarm",
            table: "AlarmRules",
            type: "decimal(14,4)",
            precision: 14,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MinimumValue",
            schema: "Alarm",
            table: "AlarmRules",
            type: "decimal(14,4)",
            precision: 14,
            scale: 4,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MaximumValue",
            schema: "Alarm",
            table: "AlarmRules");

        migrationBuilder.DropColumn(
            name: "MinimumValue",
            schema: "Alarm",
            table: "AlarmRules");

        migrationBuilder.AlterColumn<string>(
            name: "Operator",
            schema: "Alarm",
            table: "AlarmRules",
            type: "nvarchar(10)",
            maxLength: 10,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(20)",
            oldMaxLength: 20,
            oldNullable: true);
    }
}
