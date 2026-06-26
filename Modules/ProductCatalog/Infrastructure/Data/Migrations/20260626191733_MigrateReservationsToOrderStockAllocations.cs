using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductCatalog.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateReservationsToOrderStockAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderStockAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductVariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    HeldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RestoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    HoldExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LegacySessionKey = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStockAllocations", x => x.Id);
                });

            migrationBuilder.Sql("""
                UPDATE "Variants" AS v
                SET "StockQuantity" = v."StockQuantity" + orphan."Quantity"
                FROM (
                    SELECT "VariantId", SUM("Quantity") AS "Quantity"
                    FROM "StockReservations"
                    WHERE "Status" = 'Reserved'
                        AND "OrderId" IS NULL
                        AND NOT ("SessionKey" ~* '^order:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')
                    GROUP BY "VariantId"
                ) AS orphan
                WHERE v."Id" = orphan."VariantId";
                """);

            migrationBuilder.Sql("""
                WITH legacy AS (
                    SELECT
                        "Id",
                        "VariantId",
                        "Quantity",
                        "SessionKey",
                        "OrderId" AS "ExistingOrderId",
                        CASE
                            WHEN "OrderId" IS NOT NULL THEN "OrderId"
                            WHEN "SessionKey" ~* '^order:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                                THEN substring("SessionKey" from 7)::uuid
                            ELSE NULL
                        END AS "DerivedOrderId",
                        "Status",
                        "ExpiresAt",
                        "CreatedAt",
                        "UpdatedAt"
                    FROM "StockReservations"
                ),
                ranked AS (
                    SELECT
                        *,
                        CASE
                            WHEN "Status" = 'Reserved' AND "DerivedOrderId" IS NULL THEN 'Released'
                            WHEN "Status" = 'Reserved' THEN 'Held'
                            WHEN "Status" = 'Confirmed' THEN 'Committed'
                            WHEN "Status" = 'Released' AND "ExistingOrderId" IS NOT NULL THEN 'Restored'
                            ELSE 'Released'
                        END AS "FinalState",
                        CASE
                            WHEN "Status" = 'Reserved' AND "DerivedOrderId" IS NOT NULL THEN 4
                            WHEN "Status" = 'Confirmed' THEN 3
                            WHEN "Status" = 'Released' AND "ExistingOrderId" IS NOT NULL THEN 2
                            ELSE 1
                        END AS "StateRank"
                    FROM legacy
                ),
                chosen AS (
                    SELECT r.*
                    FROM ranked r
                    INNER JOIN (
                        SELECT "DerivedOrderId", "VariantId", MAX("StateRank") AS "StateRank"
                        FROM ranked
                        WHERE "DerivedOrderId" IS NOT NULL
                        GROUP BY "DerivedOrderId", "VariantId"
                    ) winner
                        ON winner."DerivedOrderId" = r."DerivedOrderId"
                        AND winner."VariantId" = r."VariantId"
                        AND winner."StateRank" = r."StateRank"
                )
                INSERT INTO "OrderStockAllocations" (
                    "Id",
                    "OrderId",
                    "ProductVariantId",
                    "Quantity",
                    "State",
                    "HeldAt",
                    "CommittedAt",
                    "ReleasedAt",
                    "RestoredAt",
                    "HoldExpiresAt",
                    "LegacySessionKey",
                    "CreatedAt",
                    "UpdatedAt")
                SELECT
                    (array_agg("Id" ORDER BY "CreatedAt" NULLS LAST, "UpdatedAt" NULLS LAST, "Id"))[1],
                    "DerivedOrderId",
                    "VariantId",
                    SUM("Quantity")::integer,
                    CASE MAX("StateRank")
                        WHEN 4 THEN 'Held'
                        WHEN 3 THEN 'Committed'
                        WHEN 2 THEN 'Restored'
                        ELSE 'Released'
                    END,
                    MIN(COALESCE("CreatedAt", "UpdatedAt", NOW())),
                    CASE WHEN MAX("StateRank") IN (2, 3)
                        THEN MAX(COALESCE("UpdatedAt", "CreatedAt", NOW()))
                        ELSE NULL
                    END,
                    CASE WHEN MAX("StateRank") = 1
                        THEN MAX(COALESCE("UpdatedAt", "CreatedAt", NOW()))
                        ELSE NULL
                    END,
                    CASE WHEN MAX("StateRank") = 2
                        THEN MAX(COALESCE("UpdatedAt", "CreatedAt", NOW()))
                        ELSE NULL
                    END,
                    CASE WHEN MAX("StateRank") = 4 THEN MAX("ExpiresAt") ELSE NULL END,
                    (array_agg("SessionKey" ORDER BY "CreatedAt" NULLS LAST, "UpdatedAt" NULLS LAST, "Id"))[1],
                    MIN("CreatedAt"),
                    MAX("UpdatedAt")
                FROM chosen
                GROUP BY "DerivedOrderId", "VariantId";
                """);

            migrationBuilder.Sql("""
                WITH legacy AS (
                    SELECT
                        "Id",
                        "VariantId",
                        "Quantity",
                        "SessionKey",
                        "OrderId",
                        "Status",
                        "ExpiresAt",
                        "CreatedAt",
                        "UpdatedAt"
                    FROM "StockReservations"
                    WHERE "OrderId" IS NULL
                        AND NOT ("SessionKey" ~* '^order:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$')
                )
                INSERT INTO "OrderStockAllocations" (
                    "Id",
                    "OrderId",
                    "ProductVariantId",
                    "Quantity",
                    "State",
                    "HeldAt",
                    "CommittedAt",
                    "ReleasedAt",
                    "RestoredAt",
                    "HoldExpiresAt",
                    "LegacySessionKey",
                    "CreatedAt",
                    "UpdatedAt")
                SELECT
                    "Id",
                    NULL,
                    "VariantId",
                    "Quantity",
                    CASE
                        WHEN "Status" = 'Confirmed' THEN 'Committed'
                        ELSE 'Released'
                    END,
                    COALESCE("CreatedAt", "UpdatedAt", NOW()),
                    CASE WHEN "Status" = 'Confirmed' THEN COALESCE("UpdatedAt", "CreatedAt", NOW()) ELSE NULL END,
                    CASE WHEN "Status" IN ('Reserved', 'Released') THEN COALESCE("UpdatedAt", NOW()) ELSE NULL END,
                    NULL,
                    NULL,
                    "SessionKey",
                    "CreatedAt",
                    "UpdatedAt"
                FROM legacy;
                """);

            migrationBuilder.DropTable(
                name: "StockReservations");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStockAllocations_LegacySessionKey_State",
                table: "OrderStockAllocations",
                columns: new[] { "LegacySessionKey", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStockAllocations_OrderId_ProductVariantId",
                table: "OrderStockAllocations",
                columns: new[] { "OrderId", "ProductVariantId" },
                unique: true,
                filter: "\"OrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStockAllocations_ProductVariantId_State",
                table: "OrderStockAllocations",
                columns: new[] { "ProductVariantId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStockAllocations_State_HoldExpiresAt",
                table: "OrderStockAllocations",
                columns: new[] { "State", "HoldExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SessionKey = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReservations", x => x.Id);
                });

            migrationBuilder.Sql("""
                INSERT INTO "StockReservations" (
                    "Id",
                    "CreatedAt",
                    "ExpiresAt",
                    "OrderId",
                    "Quantity",
                    "SessionKey",
                    "Status",
                    "UpdatedAt",
                    "VariantId")
                SELECT
                    "Id",
                    "CreatedAt",
                    COALESCE("HoldExpiresAt", "ReleasedAt", "RestoredAt", "CommittedAt", "HeldAt", "CreatedAt", NOW()),
                    "OrderId",
                    "Quantity",
                    COALESCE("LegacySessionKey", CASE
                        WHEN "OrderId" IS NOT NULL THEN 'order:' || "OrderId"::text
                        ELSE 'legacy-allocation:' || "Id"::text
                    END),
                    CASE "State"
                        WHEN 'Held' THEN 'Reserved'
                        WHEN 'Committed' THEN 'Confirmed'
                        ELSE 'Released'
                    END,
                    "UpdatedAt",
                    "ProductVariantId"
                FROM "OrderStockAllocations";
                """);

            migrationBuilder.DropTable(
                name: "OrderStockAllocations");

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_SessionKey_Status",
                table: "StockReservations",
                columns: new[] { "SessionKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_Status_ExpiresAt",
                table: "StockReservations",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockReservations_VariantId_Status",
                table: "StockReservations",
                columns: new[] { "VariantId", "Status" });
        }
    }
}
