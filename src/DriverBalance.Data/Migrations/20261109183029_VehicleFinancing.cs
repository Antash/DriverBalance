using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverBalance.Data.Migrations
{
    /// <inheritdoc />
    public partial class VehicleFinancing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FinancingAmount",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinancingBalloon",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinancingFrequency",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinancingPayments",
                table: "Vehicles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FinancingStart",
                table: "Vehicles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinancingAmount",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FinancingBalloon",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FinancingFrequency",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FinancingPayments",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "FinancingStart",
                table: "Vehicles");
        }
    }
}
