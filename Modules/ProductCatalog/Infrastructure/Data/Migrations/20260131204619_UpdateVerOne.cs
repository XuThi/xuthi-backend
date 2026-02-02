using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVerOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariantOptions_Products_ProductId1",
                table: "ProductVariantOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductVariantOptions_VariantOptions_DimensionId",
                table: "ProductVariantOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_VariantOptionSelections_Variants_VariantId1",
                table: "VariantOptionSelections");

            migrationBuilder.DropIndex(
                name: "IX_VariantOptionSelections_VariantId1",
                table: "VariantOptionSelections");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariantOptions_DimensionId",
                table: "ProductVariantOptions");

            migrationBuilder.DropIndex(
                name: "IX_ProductVariantOptions_ProductId1",
                table: "ProductVariantOptions");

            migrationBuilder.DropColumn(
                name: "DimensionId",
                table: "VariantOptionValues");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "VariantOptionValues");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "VariantOptionSelections");

            migrationBuilder.DropColumn(
                name: "VariantId1",
                table: "VariantOptionSelections");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "VariantOptions");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "ProductVariantOptions");

            migrationBuilder.DropColumn(
                name: "ProductId1",
                table: "ProductVariantOptions");

            migrationBuilder.AlterColumn<string>(
                name: "DimensionId",
                table: "VariantOptionSelections",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DimensionId",
                table: "ProductVariantOptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DimensionId",
                table: "VariantOptionValues",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "VariantOptionValues",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DimensionId",
                table: "VariantOptionSelections",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "VariantOptionSelections",
                type: "character varying(34)",
                maxLength: 34,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "VariantId1",
                table: "VariantOptionSelections",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "VariantOptions",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DimensionId",
                table: "ProductVariantOptions",
                type: "character varying(50)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "ProductVariantOptions",
                type: "character varying(21)",
                maxLength: 21,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId1",
                table: "ProductVariantOptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VariantOptionSelections_VariantId1",
                table: "VariantOptionSelections",
                column: "VariantId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariantOptions_DimensionId",
                table: "ProductVariantOptions",
                column: "DimensionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVariantOptions_ProductId1",
                table: "ProductVariantOptions",
                column: "ProductId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariantOptions_Products_ProductId1",
                table: "ProductVariantOptions",
                column: "ProductId1",
                principalTable: "Products",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductVariantOptions_VariantOptions_DimensionId",
                table: "ProductVariantOptions",
                column: "DimensionId",
                principalTable: "VariantOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_VariantOptionSelections_Variants_VariantId1",
                table: "VariantOptionSelections",
                column: "VariantId1",
                principalTable: "Variants",
                principalColumn: "Id");
        }
    }
}
