using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverBalance.Data.Migrations
{
    /// <inheritdoc />
    public partial class VehicleOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedResaleValue",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PurchaseDate",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ResaleEstimateDate",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimatedResaleValue",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ResaleEstimateDate",
                table: "Vehicles");
        }
    }
}
