using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogTimeStampIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The index may already have been added by hand to speed up a dev database, so only create it if missing.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AuditLogs_TimeStamp_AuditLogId' AND object_id = OBJECT_ID('dbo.AuditLogs'))
                    CREATE INDEX IX_AuditLogs_TimeStamp_AuditLogId ON dbo.AuditLogs (TimeStamp DESC, AuditLogId DESC);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TimeStamp_AuditLogId",
                table: "AuditLogs");
        }
    }
}
