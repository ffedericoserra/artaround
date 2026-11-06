using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtAround.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdatedAtTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION set_updated_at() RETURNS trigger AS $$
                BEGIN
                    NEW.updated_at = now();
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER museums_set_updated_at
                    BEFORE UPDATE ON museums
                    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

                CREATE TRIGGER contents_set_updated_at
                    BEFORE UPDATE ON contents
                    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER museums_set_updated_at ON museums;
                DROP TRIGGER contents_set_updated_at ON contents;
                DROP FUNCTION set_updated_at();
                """);
        }
    }
}
