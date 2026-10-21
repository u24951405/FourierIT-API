using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class EveryDocumentExpires : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every document expires. Documents stored as "never expires" (the maximum date), either because their
            // type was set to never expire or because they had no certification date, get a real expiry date:
            // the type's months from the certification date when the type counts from it and one exists,
            // otherwise from the upload date. The hourly expiry sweep then marks any that are already past it.
            migrationBuilder.Sql(@"
UPDATE d
SET d.ExpiryDate = DATEADD(month,
        CASE WHEN t.ValidityMonths > 0 THEN t.ValidityMonths ELSE 1 END,
        CASE WHEN t.ValidityBasis = 0 AND c.LatestCertification IS NOT NULL
             THEN c.LatestCertification
             ELSE TODATETIMEOFFSET(d.UploadedDate, '+00:00') END)
FROM Documents d
JOIN DocumentTypes t ON t.DocumentTypeId = d.DocumentTypeId
OUTER APPLY (SELECT MAX(cd.CertificationDate) AS LatestCertification
             FROM CertificationDetails cd WHERE cd.DocumentId = d.DocumentId) c
WHERE t.NeverExpires = 1 OR d.ExpiryDate >= '9999-01-01';");

            migrationBuilder.DropColumn(
                name: "NeverExpires",
                table: "DocumentTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NeverExpires",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 1,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 2,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 3,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 4,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 5,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 6,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 7,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 8,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 9,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 10,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 11,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 12,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 13,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 14,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 15,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 16,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 17,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 18,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 19,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 20,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 21,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                table: "DocumentTypes",
                keyColumn: "DocumentTypeId",
                keyValue: 22,
                columns: new string[0],
                values: new object[0]);
        }
    }
}
