using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AccessTokenPerRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessTokens_UserId",
                table: "AccessTokens");

            migrationBuilder.CreateIndex(
                name: "IX_AccessTokens_UserId",
                table: "AccessTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessTokens_UserId",
                table: "AccessTokens");

            migrationBuilder.CreateIndex(
                name: "IX_AccessTokens_UserId",
                table: "AccessTokens",
                column: "UserId",
                unique: true);
        }
    }
}
