using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AccessFollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EscalatedAt",
                table: "InstitutionEnquiryRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtensionReason",
                table: "InstitutionEnquiryRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtensionRequestedAt",
                table: "InstitutionEnquiryRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExtensionRequestedUntil",
                table: "InstitutionEnquiryRequests",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExtensionRespondedAt",
                table: "InstitutionEnquiryRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtensionResponseNote",
                table: "InstitutionEnquiryRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtensionStatus",
                table: "InstitutionEnquiryRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnerReminderSentAt",
                table: "InstitutionEnquiryRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionNote",
                table: "EnquiryFlags",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResolvedAt",
                table: "EnquiryFlags",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiryReminderSentAt",
                table: "AccessTokens",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Key", "Description", "Value" },
                values: new object[,]
                {
                    { "AccessExpiryReminderHours", "How long before an institution's access to documents ends that it is emailed a reminder.", "24" },
                    { "MaxAccessExtensionDays", "The most extra time an institution can ask for when it needs approved documents for longer.", "7" },
                    { "OwnerReminderAfterHours", "How long a document request can wait for an answer before the owner (or department admin) is reminded.", "48" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "AccessExpiryReminderHours");

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "MaxAccessExtensionDays");

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "OwnerReminderAfterHours");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "InstitutionEnquiryRequests");

            migrationBuilder.DropColumn(
                name: "ExtensionReason",
                table: "InstitutionEnquiryRequests");

            migrationBuilder.DropColumn(
                name: "ExtensionRequestedAt",
                table: "InstitutionEnquiryRequests");

            migrationBuilder.DropColumn(
                name: "ExtensionRequestedUntil",
                table: "InstitutionEnquiryRequests");

            migrationBuilder.DropColumn(
                name: "ExtensionRespondedAt",
                table: "InstitutionEnquiryRequests");

            migrationBuilder.DropColumn(
                name: "ExtensionResponseNote",
                table: "InstitutionEnquiryRequests");

            migrationBuilder.DropColumn(
                name: "ExtensionStatus",
                table: "InstitutionEnquiryRequests");

            migrationBuilder.DropColumn(
                name: "OwnerReminderSentAt",
                table: "InstitutionEnquiryRequests");

            migrationBuilder.DropColumn(
                name: "ResolutionNote",
                table: "EnquiryFlags");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "EnquiryFlags");

            migrationBuilder.DropColumn(
                name: "ExpiryReminderSentAt",
                table: "AccessTokens");
        }
    }
}
