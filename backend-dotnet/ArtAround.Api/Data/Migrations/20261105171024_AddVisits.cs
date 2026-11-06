using System;
using System.Collections.Generic;
using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArtAround.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:answer_type", "multiple-choice,open")
                .Annotation("Npgsql:Enum:block_type", "artwork,questions")
                .Annotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .Annotation("Npgsql:Enum:language", "en,it")
                .Annotation("Npgsql:Enum:license", "CC-BY,CC-BY-NC,CC-BY-SA,Copyright,Public Domain")
                .Annotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .Annotation("Npgsql:Enum:tone", "complex,easy,medium")
                .Annotation("Npgsql:Enum:visit_length", "deep,normal,quick")
                .Annotation("Npgsql:Enum:visit_type", "standard,synchronized")
                .OldAnnotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .OldAnnotation("Npgsql:Enum:language", "en,it")
                .OldAnnotation("Npgsql:Enum:license", "CC-BY,CC-BY-NC,CC-BY-SA,Copyright,Public Domain")
                .OldAnnotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .OldAnnotation("Npgsql:Enum:tone", "complex,easy,medium");

            migrationBuilder.CreateTable(
                name: "visits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    museum_id = table.Column<long>(type: "bigint", nullable: false),
                    creator_id = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<VisitType>(type: "visit_type", nullable: false, defaultValue: VisitType.Standard),
                    length = table.Column<VisitLength>(type: "visit_length", nullable: false, defaultValue: VisitLength.Normal),
                    is_public = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visits", x => x.id);
                    table.ForeignKey(
                        name: "fk_visits_museums_museum_id",
                        column: x => x.museum_id,
                        principalTable: "museums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_visits_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "saved_visits",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    visit_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_visits", x => new { x.user_id, x.visit_id });
                    table.ForeignKey(
                        name: "fk_saved_visits_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_saved_visits_visits_visit_id",
                        column: x => x.visit_id,
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visit_blocks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    visit_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<BlockType>(type: "block_type", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit_blocks", x => x.id);
                    table.ForeignKey(
                        name: "fk_visit_blocks_visits_visit_id",
                        column: x => x.visit_id,
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visit_quiz_questions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    visit_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    options = table.Column<List<string>>(type: "text[]", nullable: false),
                    correct_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit_quiz_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_visit_quiz_questions_visits_visit_id",
                        column: x => x.visit_id,
                        principalTable: "visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visit_block_items",
                columns: table => new
                {
                    block_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    next_directions = table.Column<string>(type: "text", nullable: false),
                    prev_directions = table.Column<string>(type: "text", nullable: false),
                    override_image = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit_block_items", x => new { x.block_id, x.position });
                    table.ForeignKey(
                        name: "fk_visit_block_items_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_visit_block_items_visit_blocks_block_id",
                        column: x => x.block_id,
                        principalTable: "visit_blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visit_block_questions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    block_id = table.Column<long>(type: "bigint", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    prompt = table.Column<string>(type: "text", nullable: false),
                    answer_type = table.Column<AnswerType>(type: "answer_type", nullable: false),
                    options = table.Column<List<string>>(type: "text[]", nullable: false),
                    correct_index = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_visit_block_questions", x => x.id);
                    table.ForeignKey(
                        name: "fk_visit_block_questions_visit_blocks_block_id",
                        column: x => x.block_id,
                        principalTable: "visit_blocks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_saved_visits_visit_id",
                table: "saved_visits",
                column: "visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_visit_block_items_item_id",
                table: "visit_block_items",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_visit_block_questions_block_id_position",
                table: "visit_block_questions",
                columns: new[] { "block_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visit_blocks_visit_id_position",
                table: "visit_blocks",
                columns: new[] { "visit_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visit_quiz_questions_visit_id_position",
                table: "visit_quiz_questions",
                columns: new[] { "visit_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_visits_creator_id",
                table: "visits",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "ix_visits_museum_id_is_public",
                table: "visits",
                columns: new[] { "museum_id", "is_public" });

            migrationBuilder.CreateIndex(
                name: "ix_visits_slug",
                table: "visits",
                column: "slug");

            migrationBuilder.Sql("""
                CREATE TRIGGER visits_set_updated_at
                    BEFORE UPDATE ON visits
                    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "saved_visits");

            migrationBuilder.DropTable(
                name: "visit_block_items");

            migrationBuilder.DropTable(
                name: "visit_block_questions");

            migrationBuilder.DropTable(
                name: "visit_quiz_questions");

            migrationBuilder.DropTable(
                name: "visit_blocks");

            migrationBuilder.DropTable(
                name: "visits");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .Annotation("Npgsql:Enum:language", "en,it")
                .Annotation("Npgsql:Enum:license", "CC-BY,CC-BY-NC,CC-BY-SA,Copyright,Public Domain")
                .Annotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .Annotation("Npgsql:Enum:tone", "complex,easy,medium")
                .OldAnnotation("Npgsql:Enum:answer_type", "multiple-choice,open")
                .OldAnnotation("Npgsql:Enum:block_type", "artwork,questions")
                .OldAnnotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .OldAnnotation("Npgsql:Enum:language", "en,it")
                .OldAnnotation("Npgsql:Enum:license", "CC-BY,CC-BY-NC,CC-BY-SA,Copyright,Public Domain")
                .OldAnnotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .OldAnnotation("Npgsql:Enum:tone", "complex,easy,medium")
                .OldAnnotation("Npgsql:Enum:visit_length", "deep,normal,quick")
                .OldAnnotation("Npgsql:Enum:visit_type", "standard,synchronized");
        }
    }
}
