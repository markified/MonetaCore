using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonetaCore.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationEventCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "IntegrationEvents",
                type: "varchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEvents_CorrelationId",
                table: "IntegrationEvents",
                column: "CorrelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IntegrationEvents_CorrelationId",
                table: "IntegrationEvents");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "IntegrationEvents");
        }
    }
}
