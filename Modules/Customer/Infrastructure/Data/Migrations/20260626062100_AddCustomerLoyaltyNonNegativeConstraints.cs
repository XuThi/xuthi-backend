using Customer.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customer.Infrastructure.Data.Migrations
{
    [DbContext(typeof(CustomerDbContext))]
    [Migration("20260626062100_AddCustomerLoyaltyNonNegativeConstraints")]
    public partial class AddCustomerLoyaltyNonNegativeConstraints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_LoyaltyPoints_NonNegative",
                table: "Customers",
                sql: "\"LoyaltyPoints\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_TotalLoyaltySpend_NonNegative",
                table: "Customers",
                sql: "\"TotalLoyaltySpend\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Customers_TotalOrders_NonNegative",
                table: "Customers",
                sql: "\"TotalOrders\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoyaltyHistory_PointsBalanceAfter_NonNegative",
                table: "LoyaltyHistory",
                sql: "\"PointsBalanceAfter\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoyaltyHistory_TotalLoyaltySpendAfter_NonNegative",
                table: "LoyaltyHistory",
                sql: "\"TotalLoyaltySpendAfter\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LoyaltyHistory_TotalOrdersAfter_NonNegative",
                table: "LoyaltyHistory",
                sql: "\"TotalOrdersAfter\" >= 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LoyaltyHistory_TotalOrdersAfter_NonNegative",
                table: "LoyaltyHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LoyaltyHistory_TotalLoyaltySpendAfter_NonNegative",
                table: "LoyaltyHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LoyaltyHistory_PointsBalanceAfter_NonNegative",
                table: "LoyaltyHistory");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_TotalOrders_NonNegative",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_TotalLoyaltySpend_NonNegative",
                table: "Customers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Customers_LoyaltyPoints_NonNegative",
                table: "Customers");
        }
    }
}
