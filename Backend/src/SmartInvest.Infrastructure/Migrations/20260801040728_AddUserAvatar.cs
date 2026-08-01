using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartInvest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "AvatarContent",
                table: "AspNetUsers",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarContentType",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AvatarContent",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AvatarContentType",
                table: "AspNetUsers");
        }
    }
}
