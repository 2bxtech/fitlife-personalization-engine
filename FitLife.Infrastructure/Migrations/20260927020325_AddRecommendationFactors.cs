using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecommendationFactors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FactorsJson",
                table: "Recommendations",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FactorsJson",
                table: "Recommendations");
        }
    }
}
