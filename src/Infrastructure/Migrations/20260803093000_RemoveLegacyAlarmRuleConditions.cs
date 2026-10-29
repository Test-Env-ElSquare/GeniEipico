using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(MesDbContext))]
[Migration("20260803093000_RemoveLegacyAlarmRuleConditions")]
public partial class RemoveLegacyAlarmRuleConditions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ConditionType",
            schema: "Alarm",
            table: "AlarmRules");

        migrationBuilder.DropColumn(
            name: "DurationMin",
            schema: "Alarm",
            table: "AlarmRules");

        migrationBuilder.DropColumn(
            name: "FrequencyCount",
            schema: "Alarm",
            table: "AlarmRules");

        migrationBuilder.DropColumn(
            name: "FrequencyWindowMin",
            schema: "Alarm",
            table: "AlarmRules");

        migrationBuilder.DropColumn(
            name: "Operator",
            schema: "Alarm",
            table: "AlarmRules");

        migrationBuilder.DropColumn(
            name: "ThresholdValue",
            schema: "Alarm",
            table: "AlarmRules");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ConditionType",
            schema: "Alarm",
            table: "AlarmRules",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "threshold");

        migrationBuilder.AddColumn<int>(
            name: "DurationMin",
            schema: "Alarm",
            table: "AlarmRules",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FrequencyCount",
            schema: "Alarm",
            table: "AlarmRules",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "FrequencyWindowMin",
            schema: "Alarm",
            table: "AlarmRules",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Operator",
            schema: "Alarm",
            table: "AlarmRules",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "ThresholdValue",
            schema: "Alarm",
            table: "AlarmRules",
            type: "decimal(14,4)",
            precision: 14,
            scale: 4,
            nullable: true);
    }
}
