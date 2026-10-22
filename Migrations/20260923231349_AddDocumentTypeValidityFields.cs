using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTypeValidityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeverExpires",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ValidityBasis",
                table: "DocumentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ValidityMonths",
                table: "DocumentTypes",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "WarningDays",
                table: "DocumentTypes",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 1,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 2,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 3,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 4,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 5,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 6,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 7,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 8,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 9,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 10,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 11,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 12,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 13,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 14,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 15,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 16,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 17,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 18,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 19,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 20,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 21,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 22,
                columns: new[] { "ValidityMonths", "WarningDays" },
                values: new object[] { 3, 30 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeverExpires",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "ValidityBasis",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "ValidityMonths",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "WarningDays",
                table: "DocumentTypes");
        }
    }
}
