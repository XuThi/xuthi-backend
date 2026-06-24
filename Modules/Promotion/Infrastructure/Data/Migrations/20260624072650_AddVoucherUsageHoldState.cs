using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Promotion.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherUsageHoldState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "VoucherUsages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                table: "VoucherUsages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "VoucherUsages",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.Sql("""
                UPDATE "VoucherUsages"
                SET "FinalizedAt" = "UsedAt"
                WHERE "FinalizedAt" IS NULL
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "VoucherUsages");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "VoucherUsages");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "VoucherUsages");
        }
    }
}
