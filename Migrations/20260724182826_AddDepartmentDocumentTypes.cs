using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentDocumentTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepartmentDocumentTypes",
                columns: table => new
                {
                    DepartmentDocumentTypeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentDocumentTypes", x => x.DepartmentDocumentTypeId);
                    table.ForeignKey(
                        name: "FK_DepartmentDocumentTypes_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "DepartmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepartmentDocumentTypes_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "DocumentTypeId");
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "DepartmentId", "BranchId", "CreatedAt", "DepartmentName" },
                values: new object[,]
                {
                    { 1, 1, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8811), new TimeSpan(0, 0, 0, 0, 0)), "Fourier IT Innovation" },
                    { 2, 1, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8815), new TimeSpan(0, 0, 0, 0, 0)), "Fourier-E Consultation" },
                    { 3, 1, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8816), new TimeSpan(0, 0, 0, 0, 0)), "RQTech" },
                    { 4, 1, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8818), new TimeSpan(0, 0, 0, 0, 0)), "Fourier Recruitment" }
                });

            migrationBuilder.InsertData(
                table: "DepartmentDocumentTypes",
                columns: new[] { "DepartmentDocumentTypeId", "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8863), new TimeSpan(0, 0, 0, 0, 0)), 1, 1, true },
                    { 2, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8865), new TimeSpan(0, 0, 0, 0, 0)), 1, 2, false },
                    { 3, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8867), new TimeSpan(0, 0, 0, 0, 0)), 1, 3, false },
                    { 4, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8869), new TimeSpan(0, 0, 0, 0, 0)), 1, 7, true },
                    { 5, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8870), new TimeSpan(0, 0, 0, 0, 0)), 1, 8, false },
                    { 6, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8873), new TimeSpan(0, 0, 0, 0, 0)), 1, 10, false },
                    { 7, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8874), new TimeSpan(0, 0, 0, 0, 0)), 1, 11, false },
                    { 8, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8876), new TimeSpan(0, 0, 0, 0, 0)), 1, 12, false },
                    { 9, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8880), new TimeSpan(0, 0, 0, 0, 0)), 2, 1, true },
                    { 10, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8883), new TimeSpan(0, 0, 0, 0, 0)), 2, 4, true },
                    { 11, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8885), new TimeSpan(0, 0, 0, 0, 0)), 2, 5, false },
                    { 12, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8887), new TimeSpan(0, 0, 0, 0, 0)), 2, 7, true },
                    { 13, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8889), new TimeSpan(0, 0, 0, 0, 0)), 2, 10, false },
                    { 14, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8891), new TimeSpan(0, 0, 0, 0, 0)), 2, 11, true },
                    { 15, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8892), new TimeSpan(0, 0, 0, 0, 0)), 2, 12, true },
                    { 16, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8894), new TimeSpan(0, 0, 0, 0, 0)), 2, 13, true },
                    { 17, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8895), new TimeSpan(0, 0, 0, 0, 0)), 3, 1, true },
                    { 18, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8898), new TimeSpan(0, 0, 0, 0, 0)), 3, 2, false },
                    { 19, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8899), new TimeSpan(0, 0, 0, 0, 0)), 3, 7, true },
                    { 20, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8901), new TimeSpan(0, 0, 0, 0, 0)), 3, 8, false },
                    { 21, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8902), new TimeSpan(0, 0, 0, 0, 0)), 3, 11, true },
                    { 22, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8904), new TimeSpan(0, 0, 0, 0, 0)), 3, 13, true },
                    { 23, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8906), new TimeSpan(0, 0, 0, 0, 0)), 3, 14, true },
                    { 24, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8908), new TimeSpan(0, 0, 0, 0, 0)), 3, 18, true },
                    { 25, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8909), new TimeSpan(0, 0, 0, 0, 0)), 4, 1, true },
                    { 26, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8911), new TimeSpan(0, 0, 0, 0, 0)), 4, 2, false },
                    { 27, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8912), new TimeSpan(0, 0, 0, 0, 0)), 4, 3, false },
                    { 28, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8914), new TimeSpan(0, 0, 0, 0, 0)), 4, 4, true },
                    { 29, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8915), new TimeSpan(0, 0, 0, 0, 0)), 4, 5, false },
                    { 30, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8917), new TimeSpan(0, 0, 0, 0, 0)), 4, 7, true },
                    { 31, new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8919), new TimeSpan(0, 0, 0, 0, 0)), 4, 10, false }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentDocumentTypes_DepartmentId",
                table: "DepartmentDocumentTypes",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentDocumentTypes_DocumentTypeId",
                table: "DepartmentDocumentTypes",
                column: "DocumentTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentDocumentTypes");

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 4);
        }
    }
}
