using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionSessionTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstitutionSessionTokens",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<int>(type: "int", nullable: false),
                    TokenString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionSessionTokens", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_InstitutionSessionTokens_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "InstitutionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2644), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2648), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2650), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2653), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2655), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2659), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2660), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2662), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2665), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2668), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2685), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 12,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2687), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 13,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2689), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 14,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2691), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 15,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2692), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 16,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2694), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 17,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2696), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 18,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2699), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 19,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2701), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 20,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2704), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 21,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2706), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 22,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2708), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 23,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2709), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 24,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2711), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 25,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2713), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 26,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2715), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 27,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2717), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 28,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2718), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 29,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2720), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 30,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2722), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 31,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2723), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 32,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2725), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 33,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2727), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 34,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2730), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 35,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2732), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 36,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2734), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 37,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2736), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 38,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2737), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 39,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2739), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 40,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2742), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 41,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2743), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 42,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2745), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 43,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2747), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 44,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2749), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 45,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2751), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 46,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2752), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 47,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2754), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 48,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2756), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2570), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2574), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2576), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "DepartmentId",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 23, 1, 24, 535, DateTimeKind.Unspecified).AddTicks(2578), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_InstitutionSessionTokens_InstitutionId",
                table: "InstitutionSessionTokens",
                column: "InstitutionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstitutionSessionTokens");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "AD");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "CO");

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
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(827), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(829), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(831), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(834), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 11,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(860), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 12,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(862), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 13,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(864), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 14,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(866), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 15,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(867), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 16,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(869), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 17,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(871), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 18,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(874), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 19,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(875), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 20,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(877), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 21,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(879), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 22,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(884), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 23,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(886), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 24,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(888), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 25,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(889), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 26,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(891), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 27,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(893), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 28,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(895), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 29,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(896), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 30,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(898), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 31,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(900), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 32,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(901), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 33,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(903), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 34,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(906), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 35,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(963), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 36,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(965), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 37,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(967), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 38,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(968), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 39,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(970), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 40,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(972), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 41,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(975), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 42,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(977), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 43,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(979), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 44,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(980), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 45,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(982), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 46,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(984), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 47,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(985), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.UpdateData(
                table: "DepartmentDocumentTypes",
                keyColumn: "DepartmentDocumentTypeId",
                keyValue: 48,
                column: "CreatedAt",
                value: new DateTimeOffset(new DateTime(2026, 7, 25, 20, 16, 31, 80, DateTimeKind.Unspecified).AddTicks(987), new TimeSpan(0, 0, 0, 0, 0)));

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
        }
    }
}
