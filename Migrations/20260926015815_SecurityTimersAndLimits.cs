using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class SecurityTimersAndLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedAttempts",
                table: "PendingRegistrations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OtpFailedAttempts",
                table: "InstitutionInvitations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OtpLastSentAt",
                table: "InstitutionInvitations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "InstitutionOtpExpiryMinutes",
                column: "Description",
                value: "How long the code emailed to an institution when it opens its access link stays valid.");

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "InstitutionSessionTimeoutMinutes",
                columns: new[] { "Description", "Value" },
                values: new object[] { "How long an institution stays signed in to the portal after entering its code.", "60" });

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "UserOtpExpiryMinutes",
                columns: new[] { "Description", "Value" },
                values: new object[] { "How long the code emailed when someone registers or changes their email stays valid.", "10" });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Key", "Description", "Value" },
                values: new object[,]
                {
                    { "CodeResendCooldownSeconds", "How long someone must wait before another verification code or access link can be emailed.", "60" },
                    { "DocumentAccessLinkExpiryHours", "How long the access link issued for an institution's document request stays valid.", "48" },
                    { "InstitutionInvitationExpiryDays", "How long an emailed institution access link can be used before a new one is needed.", "7" },
                    { "MaxCodeAttempts", "How many incorrect verification codes are allowed before the code is cancelled and a new one is needed.", "5" },
                    { "PasswordResetLinkExpiryMinutes", "How long a password reset link stays valid.", "60" },
                    { "StaffSessionTimeoutHours", "How long staff stay signed in before they must sign in again.", "8" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "CodeResendCooldownSeconds");

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "DocumentAccessLinkExpiryHours");

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "InstitutionInvitationExpiryDays");

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "MaxCodeAttempts");

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "PasswordResetLinkExpiryMinutes");

            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "StaffSessionTimeoutHours");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                table: "PendingRegistrations");

            migrationBuilder.DropColumn(
                name: "OtpFailedAttempts",
                table: "InstitutionInvitations");

            migrationBuilder.DropColumn(
                name: "OtpLastSentAt",
                table: "InstitutionInvitations");

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "InstitutionOtpExpiryMinutes",
                column: "Description",
                value: "Minutes before an institution portal OTP expires.");

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "InstitutionSessionTimeoutMinutes",
                columns: new[] { "Description", "Value" },
                values: new object[] { "Minutes before an institution portal session expires.", "480" });

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Key",
                keyValue: "UserOtpExpiryMinutes",
                columns: new[] { "Description", "Value" },
                values: new object[] { "Minutes before a user's email verification code (registration or email change) expires.", "15" });
        }
    }
}
