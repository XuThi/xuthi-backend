using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cart.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260623114500_AddCartStatus")]
    public partial class AddCartStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShoppingCarts_CustomerId",
                table: "ShoppingCarts");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingCarts_SessionId",
                table: "ShoppingCarts");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsumedAt",
                table: "ShoppingCarts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ShoppingCarts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCarts_CustomerId",
                table: "ShoppingCarts",
                column: "CustomerId",
                filter: "\"CustomerId\" IS NOT NULL AND \"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCarts_SessionId",
                table: "ShoppingCarts",
                column: "SessionId",
                unique: true,
                filter: "\"SessionId\" IS NOT NULL AND \"Status\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShoppingCarts_CustomerId",
                table: "ShoppingCarts");

            migrationBuilder.DropIndex(
                name: "IX_ShoppingCarts_SessionId",
                table: "ShoppingCarts");

            migrationBuilder.DropColumn(
                name: "ConsumedAt",
                table: "ShoppingCarts");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ShoppingCarts");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCarts_CustomerId",
                table: "ShoppingCarts",
                column: "CustomerId",
                filter: "\"CustomerId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCarts_SessionId",
                table: "ShoppingCarts",
                column: "SessionId",
                unique: true,
                filter: "\"SessionId\" IS NOT NULL");
        }
    }
}
