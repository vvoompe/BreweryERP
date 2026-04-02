using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BreweryERP.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBeerStyleBjcpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "beer_styles",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxAbv",
                table: "beer_styles",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxIbu",
                table: "beer_styles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxSrm",
                table: "beer_styles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinAbv",
                table: "beer_styles",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinIbu",
                table: "beer_styles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinSrm",
                table: "beer_styles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "beer_styles");

            migrationBuilder.DropColumn(
                name: "MaxAbv",
                table: "beer_styles");

            migrationBuilder.DropColumn(
                name: "MaxIbu",
                table: "beer_styles");

            migrationBuilder.DropColumn(
                name: "MaxSrm",
                table: "beer_styles");

            migrationBuilder.DropColumn(
                name: "MinAbv",
                table: "beer_styles");

            migrationBuilder.DropColumn(
                name: "MinIbu",
                table: "beer_styles");

            migrationBuilder.DropColumn(
                name: "MinSrm",
                table: "beer_styles");
        }
    }
}
