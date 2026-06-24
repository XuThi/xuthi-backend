using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Promotion.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [Migration("20260623144500_AddVoucherUsageOrderVoucherIdempotency")]
    public partial class AddVoucherUsageOrderVoucherIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VoucherUsages_OrderId_VoucherId",
                table: "VoucherUsages",
                columns: new[] { "OrderId", "VoucherId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoucherUsages_OrderId_VoucherId",
                table: "VoucherUsages");
        }
    }
}
