using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderIntakeCreatedOrderFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOrderAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceCartId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SourceCartId",
                table: "Orders",
                column: "SourceCartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_SourceCartId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedOrderAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SourceCartId",
                table: "Orders");
        }
    }
}
