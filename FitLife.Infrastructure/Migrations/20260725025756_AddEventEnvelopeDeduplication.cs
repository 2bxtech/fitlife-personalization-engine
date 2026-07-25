using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FitLife.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventEnvelopeDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventId",
                table: "Interactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interactions_EventId",
                table: "Interactions",
                column: "EventId",
                unique: true,
                filter: "[EventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Interactions_EventId",
                table: "Interactions");

            migrationBuilder.DropColumn(
                name: "EventId",
                table: "Interactions");
        }
    }
}
