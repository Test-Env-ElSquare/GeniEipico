using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductionQRejectDiff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QrejectDiff",
                schema: "RealTime",
                table: "Production",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.Sql("""
                WITH RejectReadings AS
                (
                    SELECT
                        [Id],
                        [Qreject],
                        LAG([Qreject]) OVER
                        (
                            PARTITION BY [CustomerId], [FactoryId], [LineId], [MachineId]
                            ORDER BY [TimeStamp], [Id]
                        ) AS [PreviousQreject]
                    FROM [RealTime].[Production]
                )
                UPDATE production
                SET production.[QrejectDiff] =
                    CASE
                        WHEN reading.[PreviousQreject] IS NULL THEN 0
                        WHEN reading.[Qreject] <= reading.[PreviousQreject] THEN 0
                        ELSE CONVERT(decimal(18,4), reading.[Qreject] - reading.[PreviousQreject])
                    END
                FROM [RealTime].[Production] AS production
                INNER JOIN RejectReadings AS reading
                    ON reading.[Id] = production.[Id];
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "QrejectDiff",
                schema: "RealTime",
                table: "Production",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrejectDiff",
                schema: "RealTime",
                table: "Production");
        }
    }
}
