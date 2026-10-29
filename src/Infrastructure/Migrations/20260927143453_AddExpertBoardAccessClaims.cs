using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertBoardAccessClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"INSERT INTO [Security].[UserClaims] ([UserId], [ClaimType], [ClaimValue])
                  SELECT [user].[Id], N'expertBoard', [board].[ClaimValue]
                  FROM [Security].[Users] AS [user]
                  CROSS JOIN
                  (
                      VALUES (N'1'), (N'2'), (N'3')
                  ) AS [board] ([ClaimValue])
                  WHERE NOT EXISTS
                  (
                      SELECT 1
                      FROM [Security].[UserClaims] AS [existing]
                      WHERE [existing].[UserId] = [user].[Id]
                        AND [existing].[ClaimType] = N'expertBoard'
                        AND [existing].[ClaimValue] = [board].[ClaimValue]
                  );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DELETE FROM [Security].[UserClaims]
                  WHERE [ClaimType] = N'expertBoard';");
        }
    }
}
