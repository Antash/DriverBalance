using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverBalance.Data.Migrations
{
    /// <inheritdoc />
    public partial class DemoVehicle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Vehicles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Vehicles");
        }
    }
}
