using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPayOsOrderAttemptWindowFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_SourceCartId",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "PaymentLinkUrl",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentSettlementGraceEndsAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentWindowExpiresAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SourceCartId",
                table: "Orders",
                column: "SourceCartId",
                unique: true,
                filter: "\"SourceCartId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_SourceCartId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentLinkUrl",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentSettlementGraceEndsAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentWindowExpiresAt",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SourceCartId",
                table: "Orders",
                column: "SourceCartId");
        }
    }
}
