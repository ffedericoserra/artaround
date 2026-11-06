using System;
using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArtAround.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .Annotation("Npgsql:Enum:language", "en,it")
                .Annotation("Npgsql:Enum:license", "CC-BY,CC-BY-NC,CC-BY-SA,Copyright,Public Domain")
                .Annotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .Annotation("Npgsql:Enum:tone", "complex,easy,medium")
                .OldAnnotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .OldAnnotation("Npgsql:Enum:language", "en,it")
                .OldAnnotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet");

            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    content_id = table.Column<long>(type: "bigint", nullable: false),
                    creator_id = table.Column<long>(type: "bigint", nullable: false),
                    target_audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    license = table.Column<License>(type: "license", nullable: false, defaultValue: License.CcBy),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_items", x => x.id);
                    table.CheckConstraint("items_price_nonnegative", "price >= 0");
                    table.ForeignKey(
                        name: "fk_items_contents_content_id",
                        column: x => x.content_id,
                        principalTable: "contents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_items_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "item_associated_contents",
                columns: table => new
                {
                    content_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_associated_contents", x => new { x.content_id, x.item_id });
                    table.ForeignKey(
                        name: "fk_item_associated_contents_contents_content_id",
                        column: x => x.content_id,
                        principalTable: "contents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_item_associated_contents_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "item_texts",
                columns: table => new
                {
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    tone = table.Column<Tone>(type: "tone", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_texts", x => new { x.item_id, x.tone });
                    table.ForeignKey(
                        name: "fk_item_texts_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_associated_contents_item_id",
                table: "item_associated_contents",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_content_id",
                table: "items",
                column: "content_id");

            migrationBuilder.CreateIndex(
                name: "ix_items_creator_id",
                table: "items",
                column: "creator_id");

            migrationBuilder.Sql("""
                CREATE TRIGGER items_set_updated_at
                    BEFORE UPDATE ON items
                    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_associated_contents");

            migrationBuilder.DropTable(
                name: "item_texts");

            migrationBuilder.DropTable(
                name: "items");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .Annotation("Npgsql:Enum:language", "en,it")
                .Annotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .OldAnnotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .OldAnnotation("Npgsql:Enum:language", "en,it")
                .OldAnnotation("Npgsql:Enum:license", "CC-BY,CC-BY-NC,CC-BY-SA,Copyright,Public Domain")
                .OldAnnotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .OldAnnotation("Npgsql:Enum:tone", "complex,easy,medium");
        }
    }
}
