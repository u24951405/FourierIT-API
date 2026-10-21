using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FourierIT_API.Migrations
{
    /// <inheritdoc />
    public partial class UserRoleIsIdentityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Identity now saves role assignments as UserRole itself, so the column that told the two types apart goes.
            // Assignments saved by Identity until now have no IsActiveContext; give them the model's default (true).
            migrationBuilder.Sql("UPDATE [AspNetUserRoles] SET [IsActiveContext] = 1 WHERE [IsActiveContext] IS NULL;");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "AspNetUserRoles");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActiveContext",
                table: "AspNetUserRoles",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsActiveContext",
                table: "AspNetUserRoles",
                type: "bit",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "AspNetUserRoles",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "UserRole");
        }
    }
}
