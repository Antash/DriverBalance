using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverBalance.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaintenanceWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkJson",
                table: "Expenses",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkJson",
                table: "Expenses");
        }
    }
}
