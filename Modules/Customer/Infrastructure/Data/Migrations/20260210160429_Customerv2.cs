using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Customerv2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "KeycloakUserId",
                table: "Customers",
                newName: "ExternalUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_KeycloakUserId",
                table: "Customers",
                newName: "IX_Customers_ExternalUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExternalUserId",
                table: "Customers",
                newName: "KeycloakUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_ExternalUserId",
                table: "Customers",
                newName: "IX_Customers_KeycloakUserId");
        }
    }
}
