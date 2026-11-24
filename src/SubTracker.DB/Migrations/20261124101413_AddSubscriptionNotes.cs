using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubTracker.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Subscriptions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Subscriptions");
        }
    }
}
