using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VyTSystems.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitudIdempotenciaTombstone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "solicitud_idempotencias_eliminadas",
                columns: table => new
                {
                    creado_por_usuario_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    idempotency_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    idempotency_payload_hash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    solicitud_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    eliminado_en_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitud_idempotencias_eliminadas", x => new { x.creado_por_usuario_id, x.idempotency_key });
                    table.CheckConstraint("CK_solicitud_idempotencias_eliminadas_hash_longitud", "CHAR_LENGTH(idempotency_payload_hash) = 64");
                    table.CheckConstraint("CK_solicitud_idempotencias_eliminadas_key_no_vacio", "CHAR_LENGTH(TRIM(idempotency_key)) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_solicitud_idempotencias_eliminadas_usuario_no_vacio", "CHAR_LENGTH(TRIM(creado_por_usuario_id)) BETWEEN 1 AND 36");
                    table.ForeignKey(
                        name: "FK_solicitud_idempotencias_eliminadas_asp_net_users_usuario_id",
                        column: x => x.creado_por_usuario_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.Sql(
                "ALTER TABLE `solicitud_idempotencias_eliminadas` " +
                "MODIFY COLUMN `idempotency_key` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_cs NOT NULL, " +
                "MODIFY COLUMN `idempotency_payload_hash` char(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_as_cs NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_fecha_entrega",
                table: "solicitudes",
                column: "fecha_entrega");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "solicitud_idempotencias_eliminadas");

            migrationBuilder.DropIndex(
                name: "IX_solicitudes_fecha_entrega",
                table: "solicitudes");
        }
    }
}
