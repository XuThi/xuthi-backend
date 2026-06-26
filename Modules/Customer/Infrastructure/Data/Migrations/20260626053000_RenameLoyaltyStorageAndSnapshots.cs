using System;
using Customer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.Infrastructure.Data.Migrations
{
    [DbContext(typeof(CustomerDbContext))]
    [Migration("20260626053000_RenameLoyaltyStorageAndSnapshots")]
    public partial class RenameLoyaltyStorageAndSnapshots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointsHistory_Customers_CustomerId",
                table: "PointsHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PointsHistory",
                table: "PointsHistory");

            migrationBuilder.RenameTable(
                name: "PointsHistory",
                newName: "LoyaltyHistory");

            migrationBuilder.RenameColumn(
                name: "TotalSpent",
                table: "Customers",
                newName: "TotalLoyaltySpend");

            migrationBuilder.RenameColumn(
                name: "Points",
                table: "LoyaltyHistory",
                newName: "PointsDelta");

            migrationBuilder.RenameColumn(
                name: "BalanceAfter",
                table: "LoyaltyHistory",
                newName: "PointsBalanceAfter");

            migrationBuilder.RenameIndex(
                name: "IX_PointsHistory_CustomerId",
                table: "LoyaltyHistory",
                newName: "IX_LoyaltyHistory_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_PointsHistory_CreatedAt",
                table: "LoyaltyHistory",
                newName: "IX_LoyaltyHistory_CreatedAt");

            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltySpendDelta",
                table: "LoyaltyHistory",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                table: "LoyaltyHistory",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAt",
                table: "LoyaltyHistory",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.AddColumn<int>(
                name: "TierAfter",
                table: "LoyaltyHistory",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalLoyaltySpendAfter",
                table: "LoyaltyHistory",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalOrdersAfter",
                table: "LoyaltyHistory",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "LoyaltyHistory"
                SET "OccurredAt" = COALESCE("CreatedAt", NOW());
                """);

            migrationBuilder.Sql("""
                UPDATE "LoyaltyHistory" AS h
                SET
                    "TotalLoyaltySpendAfter" = c."TotalLoyaltySpend",
                    "TotalOrdersAfter" = c."TotalOrders",
                    "TierAfter" = c."Tier"
                FROM "Customers" AS c
                WHERE h."CustomerId" = c."Id";
                """);

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyHistory_CreatedAt",
                table: "LoyaltyHistory");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyHistory_CustomerId",
                table: "LoyaltyHistory");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoyaltyHistory",
                table: "LoyaltyHistory",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyHistory_CustomerId_OccurredAt",
                table: "LoyaltyHistory",
                columns: new[] { "CustomerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyHistory_RelatedOrderId_Type",
                table: "LoyaltyHistory",
                columns: new[] { "RelatedOrderId", "Type" },
                unique: true,
                filter: "\"RelatedOrderId\" IS NOT NULL AND \"Type\" IN (1, 6)");

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyHistory_Customers_CustomerId",
                table: "LoyaltyHistory",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyHistory_Customers_CustomerId",
                table: "LoyaltyHistory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoyaltyHistory",
                table: "LoyaltyHistory");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyHistory_CustomerId_OccurredAt",
                table: "LoyaltyHistory");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyHistory_RelatedOrderId_Type",
                table: "LoyaltyHistory");

            migrationBuilder.DropColumn(
                name: "LoyaltySpendDelta",
                table: "LoyaltyHistory");

            migrationBuilder.DropColumn(
                name: "OccurredAt",
                table: "LoyaltyHistory");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "LoyaltyHistory");

            migrationBuilder.DropColumn(
                name: "TierAfter",
                table: "LoyaltyHistory");

            migrationBuilder.DropColumn(
                name: "TotalLoyaltySpendAfter",
                table: "LoyaltyHistory");

            migrationBuilder.DropColumn(
                name: "TotalOrdersAfter",
                table: "LoyaltyHistory");

            migrationBuilder.RenameColumn(
                name: "TotalLoyaltySpend",
                table: "Customers",
                newName: "TotalSpent");

            migrationBuilder.RenameColumn(
                name: "PointsDelta",
                table: "LoyaltyHistory",
                newName: "Points");

            migrationBuilder.RenameColumn(
                name: "PointsBalanceAfter",
                table: "LoyaltyHistory",
                newName: "BalanceAfter");

            migrationBuilder.RenameTable(
                name: "LoyaltyHistory",
                newName: "PointsHistory");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PointsHistory",
                table: "PointsHistory",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_PointsHistory_CreatedAt",
                table: "PointsHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PointsHistory_CustomerId",
                table: "PointsHistory",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_PointsHistory_Customers_CustomerId",
                table: "PointsHistory",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
