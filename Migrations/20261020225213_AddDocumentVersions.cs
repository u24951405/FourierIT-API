using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentVersions",
                columns: table => new
                {
                    DocumentVersionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EncryptedFile = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    FileHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FlagReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ComplianceDecision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ComplianceReviewerUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ComplianceReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ComplianceDecisionAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersions", x => x.DocumentVersionId);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_AspNetUsers_ComplianceReviewerUserId",
                        column: x => x.ComplianceReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentVersions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "DocumentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(@"
                INSERT INTO DocumentVersions
                    (DocumentId, VersionNumber, FileName, EncryptedFile, FileHash, UploadedByUserId, UploadedAt)
                SELECT d.DocumentId, db.VersionNumber, d.FileName, db.FileData, db.FileHash, d.UserId,
                       TODATETIMEOFFSET(d.UploadedDate, '+00:00')
                FROM Documents d
                INNER JOIN DocumentBlobs db ON db.DocumentId = d.DocumentId
                WHERE NOT EXISTS (
                    SELECT 1 FROM DocumentVersions v WHERE v.DocumentId = d.DocumentId
                );");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_ComplianceReviewerUserId",
                table: "DocumentVersions",
                column: "ComplianceReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_DocumentId_VersionNumber",
                table: "DocumentVersions",
                columns: new[] { "DocumentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentVersions_UploadedByUserId",
                table: "DocumentVersions",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentVersions");

        }
    }
}
