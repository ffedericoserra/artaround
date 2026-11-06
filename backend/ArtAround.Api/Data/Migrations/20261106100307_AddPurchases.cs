using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtAround.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "visits",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "items",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    price_paid = table.Column<int>(type: "integer", nullable: false),
                    purchased_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchases", x => new { x.user_id, x.item_id });
                    table.CheckConstraint("purchases_price_paid_nonnegative", "price_paid >= 0");
                    table.ForeignKey(
                        name: "fk_purchases_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchases_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchases_item_id",
                table: "purchases",
                column: "item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchases");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "visits");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "items");
        }
    }
}
