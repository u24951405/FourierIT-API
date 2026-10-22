using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class NotificationLinksAndRequesterEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Link",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "InstitutionSessionTokens",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequesterEmail",
                table: "InstitutionEnquiryRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Link",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "InstitutionSessionTokens");

            migrationBuilder.DropColumn(
                name: "RequesterEmail",
                table: "InstitutionEnquiryRequests");
        }
    }
}
