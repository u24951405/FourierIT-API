using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionIdToAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstitutionId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_InstitutionId",
                table: "AuditLogs",
                column: "InstitutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Institutions_InstitutionId",
                table: "AuditLogs",
                column: "InstitutionId",
                principalTable: "Institutions",
                principalColumn: "InstitutionId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Institutions_InstitutionId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_InstitutionId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "InstitutionId",
                table: "AuditLogs");
        }
    }
}
