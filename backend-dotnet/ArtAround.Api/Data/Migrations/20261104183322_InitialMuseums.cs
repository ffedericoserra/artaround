using System;
using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArtAround.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialMuseums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .Annotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet");

            migrationBuilder.CreateTable(
                name: "museums",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    website = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_museums", x => x.id);
                    table.CheckConstraint("museums_slug_format", "slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                });

            migrationBuilder.CreateTable(
                name: "floor_plans",
                columns: table => new
                {
                    museum_id = table.Column<long>(type: "bigint", nullable: false),
                    floor = table.Column<int>(type: "integer", nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    north = table.Column<double>(type: "double precision", nullable: false),
                    south = table.Column<double>(type: "double precision", nullable: false),
                    east = table.Column<double>(type: "double precision", nullable: false),
                    west = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_plans", x => new { x.museum_id, x.floor });
                    table.CheckConstraint("floor_plans_lat_order", "south < north");
                    table.CheckConstraint("floor_plans_lat_range", "south >= -90 AND north <= 90");
                    table.CheckConstraint("floor_plans_lng_order", "west < east");
                    table.CheckConstraint("floor_plans_lng_range", "west >= -180 AND east <= 180");
                    table.ForeignKey(
                        name: "fk_floor_plans_museums_museum_id",
                        column: x => x.museum_id,
                        principalTable: "museums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contents",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    universal_id = table.Column<string>(type: "text", nullable: false),
                    museum_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<ContentType>(type: "content_type", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "text", nullable: true),
                    year = table.Column<string>(type: "text", nullable: true),
                    image_url = table.Column<string>(type: "text", nullable: true),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    floor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contents", x => x.id);
                    table.CheckConstraint("contents_lat_range", "lat BETWEEN -90 AND 90");
                    table.CheckConstraint("contents_lng_range", "lng BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "fk_contents_floor_plans_museum_id_floor",
                        columns: x => new { x.museum_id, x.floor },
                        principalTable: "floor_plans",
                        principalColumns: new[] { "museum_id", "floor" });
                    table.ForeignKey(
                        name: "fk_contents_museums_museum_id",
                        column: x => x.museum_id,
                        principalTable: "museums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "points_of_interest",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    museum_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<PoiType>(type: "poi_type", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lng = table.Column<double>(type: "double precision", nullable: false),
                    floor = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_points_of_interest", x => x.id);
                    table.CheckConstraint("points_of_interest_lat_range", "lat BETWEEN -90 AND 90");
                    table.CheckConstraint("points_of_interest_lng_range", "lng BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "fk_points_of_interest_floor_plans_museum_id_floor",
                        columns: x => new { x.museum_id, x.floor },
                        principalTable: "floor_plans",
                        principalColumns: new[] { "museum_id", "floor" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_points_of_interest_museums_museum_id",
                        column: x => x.museum_id,
                        principalTable: "museums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_contents_museum_id_floor",
                table: "contents",
                columns: new[] { "museum_id", "floor" });

            migrationBuilder.CreateIndex(
                name: "ix_contents_museum_id_type",
                table: "contents",
                columns: new[] { "museum_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_contents_universal_id",
                table: "contents",
                column: "universal_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_museums_slug",
                table: "museums",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_points_of_interest_museum_id_floor",
                table: "points_of_interest",
                columns: new[] { "museum_id", "floor" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "contents");

            migrationBuilder.DropTable(
                name: "points_of_interest");

            migrationBuilder.DropTable(
                name: "floor_plans");

            migrationBuilder.DropTable(
                name: "museums");
        }
    }
}
