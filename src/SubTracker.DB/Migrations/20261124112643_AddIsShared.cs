using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubTracker.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddIsShared : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "Subscriptions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "Subscriptions");
        }
    }
}
