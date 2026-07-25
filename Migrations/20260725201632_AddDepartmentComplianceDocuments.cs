using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentComplianceDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First, insert the new DocumentTypes before referencing them
            migrationBuilder.InsertData(
                table: "DocumentTypes",
                columns: new[] { "DocumentTypeId", "Description", "TypeName" },
                values: new object[,]
                {
                    { 20, "Valid SARS Tax Clearance Certificate (not older than 12 months)", "SARS Tax Clearance Certificate" },
                    { 21, "Bank confirmation of account and authorized signatories", "Bank Confirmation Letter" },
                    { 22, "Declaration of beneficial owners with shareholding details", "Beneficial Ownership Declaration" }
                });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(801), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(811), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(813), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(818), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(820), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(825), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 7,
                columns: new[] { "CreatedAt", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(827), new TimeSpan(0, 0, 0, 0, 0)), true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 8,
                columns: new[] { "CreatedAt", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(829), new TimeSpan(0, 0, 0, 0, 0)), true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(831), new TimeSpan(0, 0, 0, 0, 0)), 1, 13 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 10,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(834), new TimeSpan(0, 0, 0, 0, 0)), 1, 20 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 11,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(860), new TimeSpan(0, 0, 0, 0, 0)), 1, 21, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 12,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(862), new TimeSpan(0, 0, 0, 0, 0)), 1, 22 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 13,
                columns: new[] { "CreatedAt", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(864), new TimeSpan(0, 0, 0, 0, 0)), 1, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 14,
                columns: new[] { "CreatedAt", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(866), new TimeSpan(0, 0, 0, 0, 0)), 4 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 15,
                columns: new[] { "CreatedAt", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(867), new TimeSpan(0, 0, 0, 0, 0)), 5, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 16,
                columns: new[] { "CreatedAt", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(869), new TimeSpan(0, 0, 0, 0, 0)), 7 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 17,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(871), new TimeSpan(0, 0, 0, 0, 0)), 2, 10, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 18,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(874), new TimeSpan(0, 0, 0, 0, 0)), 2, 11, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 19,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(875), new TimeSpan(0, 0, 0, 0, 0)), 2, 12 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 20,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(877), new TimeSpan(0, 0, 0, 0, 0)), 2, 13, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 21,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(879), new TimeSpan(0, 0, 0, 0, 0)), 2, 20 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 22,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(884), new TimeSpan(0, 0, 0, 0, 0)), 2, 21 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 23,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(886), new TimeSpan(0, 0, 0, 0, 0)), 2, 22 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 24,
                columns: new[] { "CreatedAt", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(888), new TimeSpan(0, 0, 0, 0, 0)), 1 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 25,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(889), new TimeSpan(0, 0, 0, 0, 0)), 3, 2, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 26,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(891), new TimeSpan(0, 0, 0, 0, 0)), 3, 7, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 27,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(893), new TimeSpan(0, 0, 0, 0, 0)), 3, 8 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 28,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(895), new TimeSpan(0, 0, 0, 0, 0)), 3, 11 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 29,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(896), new TimeSpan(0, 0, 0, 0, 0)), 3, 12, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 30,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(898), new TimeSpan(0, 0, 0, 0, 0)), 3, 13 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 31,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(900), new TimeSpan(0, 0, 0, 0, 0)), 3, 14, true });

            migrationBuilder.InsertData(
                table: "DepartmentDocumentTypes",
                columns: new[] { "DepartmentDocumentTypeId", "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[,]
                {
                    { 32, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(901), new TimeSpan(0, 0, 0, 0, 0)), 3, 18, true },
                    { 36, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(965), new TimeSpan(0, 0, 0, 0, 0)), 4, 1, true },
                    { 37, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(967), new TimeSpan(0, 0, 0, 0, 0)), 4, 2, false },
                    { 38, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(968), new TimeSpan(0, 0, 0, 0, 0)), 4, 3, false },
                    { 39, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(970), new TimeSpan(0, 0, 0, 0, 0)), 4, 4, true },
                    { 40, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(972), new TimeSpan(0, 0, 0, 0, 0)), 4, 5, false },
                    { 41, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(975), new TimeSpan(0, 0, 0, 0, 0)), 4, 7, true },
                    { 42, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(977), new TimeSpan(0, 0, 0, 0, 0)), 4, 10, false },
                    { 43, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(979), new TimeSpan(0, 0, 0, 0, 0)), 4, 11, true },
                    { 44, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(980), new TimeSpan(0, 0, 0, 0, 0)), 4, 12, true },
                    { 45, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(982), new TimeSpan(0, 0, 0, 0, 0)), 4, 13, true }
                });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(703), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(711), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(714), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(717), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.InsertData(
                table: "DepartmentDocumentTypes",
                columns: new[] { "DepartmentDocumentTypeId", "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[,]
                {
                    { 33, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(903), new TimeSpan(0, 0, 0, 0, 0)), 3, 20, true },
                    { 34, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(906), new TimeSpan(0, 0, 0, 0, 0)), 3, 21, true },
                    { 35, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(963), new TimeSpan(0, 0, 0, 0, 0)), 3, 22, true },
                    { 46, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(984), new TimeSpan(0, 0, 0, 0, 0)), 4, 20, true },
                    { 47, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(985), new TimeSpan(0, 0, 0, 0, 0)), 4, 21, true },
                    { 48, new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(987), new TimeSpan(0, 0, 0, 0, 0)), 4, 22, true }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 22);

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8863), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8865), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8867), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8869), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8870), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8873), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 7,
                columns: new[] { "CreatedAt", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8874), new TimeSpan(0, 0, 0, 0, 0)), false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 8,
                columns: new[] { "CreatedAt", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8876), new TimeSpan(0, 0, 0, 0, 0)), false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 9,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8880), new TimeSpan(0, 0, 0, 0, 0)), 2, 1 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 10,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8883), new TimeSpan(0, 0, 0, 0, 0)), 2, 4 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 11,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8885), new TimeSpan(0, 0, 0, 0, 0)), 2, 5, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 12,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8887), new TimeSpan(0, 0, 0, 0, 0)), 2, 7 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 13,
                columns: new[] { "CreatedAt", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8889), new TimeSpan(0, 0, 0, 0, 0)), 10, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 14,
                columns: new[] { "CreatedAt", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8891), new TimeSpan(0, 0, 0, 0, 0)), 11 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 15,
                columns: new[] { "CreatedAt", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8892), new TimeSpan(0, 0, 0, 0, 0)), 12, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 16,
                columns: new[] { "CreatedAt", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8894), new TimeSpan(0, 0, 0, 0, 0)), 13 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 17,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8895), new TimeSpan(0, 0, 0, 0, 0)), 3, 1, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 18,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8898), new TimeSpan(0, 0, 0, 0, 0)), 3, 2, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 19,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8899), new TimeSpan(0, 0, 0, 0, 0)), 3, 7 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 20,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8901), new TimeSpan(0, 0, 0, 0, 0)), 3, 8, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 21,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8902), new TimeSpan(0, 0, 0, 0, 0)), 3, 11 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 22,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8904), new TimeSpan(0, 0, 0, 0, 0)), 3, 13 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 23,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8906), new TimeSpan(0, 0, 0, 0, 0)), 3, 14 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 24,
                columns: new[] { "CreatedAt", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8908), new TimeSpan(0, 0, 0, 0, 0)), 18 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 25,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8909), new TimeSpan(0, 0, 0, 0, 0)), 4, 1, true });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 26,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8911), new TimeSpan(0, 0, 0, 0, 0)), 4, 2, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 27,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8912), new TimeSpan(0, 0, 0, 0, 0)), 4, 3 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 28,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8914), new TimeSpan(0, 0, 0, 0, 0)), 4, 4 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 29,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8915), new TimeSpan(0, 0, 0, 0, 0)), 4, 5, false });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 30,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8917), new TimeSpan(0, 0, 0, 0, 0)), 4, 7 });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 31,
                columns: new[] { "CreatedAt", "DepartmentId", "DocumentTypeId", "IsMandatory" },
                values: new object[] { new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8919), new TimeSpan(0, 0, 0, 0, 0)), 4, 10, false });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8811), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8815), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8816), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 24, 18, 28, 25, 286, DateTimeKind.Unspecified).AddTicks(8818), new TimeSpan(0, 0, 0, 0, 0)));
        }
    }
}
