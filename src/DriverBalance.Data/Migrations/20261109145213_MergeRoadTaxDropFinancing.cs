using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverBalance.Data.Migrations
{
    /// <inheritdoc />
    public partial class MergeRoadTaxDropFinancing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Road tax is now part of road tolls (TollsVignette); the Financing category is gone.
            migrationBuilder.Sql("UPDATE Expenses SET Category = 'TollsVignette' WHERE Category = 'RoadTax';");
            migrationBuilder.Sql("UPDATE Expenses SET Category = 'Other' WHERE Category = 'Financing';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
