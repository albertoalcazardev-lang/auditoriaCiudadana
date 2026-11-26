using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VyTSystems.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitudConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE `solicitudes` ADD COLUMN `concurrency_token` char(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_cs NULL;");

            migrationBuilder.Sql(
                "UPDATE `solicitudes` SET `concurrency_token` = LOWER(REPLACE(UUID(), '-', '')) WHERE `concurrency_token` IS NULL;");

            migrationBuilder.Sql(
                "ALTER TABLE `solicitudes` MODIFY COLUMN `concurrency_token` char(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_cs NOT NULL, " +
                "ADD CONSTRAINT `CK_solicitudes_concurrency_token_formato` CHECK (CHAR_LENGTH(`concurrency_token`) = 32 AND REGEXP_LIKE(`concurrency_token`, '^[0-9a-f]{32}$', 'c'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE `solicitudes` DROP CHECK `CK_solicitudes_concurrency_token_formato`, DROP COLUMN `concurrency_token`;");
        }
    }
}
