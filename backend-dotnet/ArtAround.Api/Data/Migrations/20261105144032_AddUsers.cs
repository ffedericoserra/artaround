using System;
using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArtAround.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .Annotation("Npgsql:Enum:language", "en,it")
                .Annotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .OldAnnotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .OldAnnotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    username = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    language = table.Column<Language>(type: "language", nullable: false, defaultValue: Language.It),
                    wallet_balance = table.Column<int>(type: "integer", nullable: false, defaultValue: 100),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("users_username_min_len", "char_length(username) >= 3");
                    table.CheckConstraint("users_username_trimmed", "username = btrim(username)");
                    table.CheckConstraint("users_wallet_nonnegative", "wallet_balance >= 0");
                });

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX users_username_lower_uq ON users (lower(username));
                CREATE UNIQUE INDEX users_email_lower_uq ON users (lower(email));

                CREATE TRIGGER users_set_updated_at
                    BEFORE UPDATE ON users
                    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .Annotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet")
                .OldAnnotation("Npgsql:Enum:content_type", "Artist,Artwork,Movement,Place")
                .OldAnnotation("Npgsql:Enum:language", "en,it")
                .OldAnnotation("Npgsql:Enum:poi_type", "bar,entrance,exit,shop,stairs,toilet");
        }
    }
}
