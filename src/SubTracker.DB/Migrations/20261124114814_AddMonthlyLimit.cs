using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SubTracker.DB.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyLimit",
                table: "Subscriptions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonthlyLimit",
                table: "Subscriptions");
        }
    }
}
