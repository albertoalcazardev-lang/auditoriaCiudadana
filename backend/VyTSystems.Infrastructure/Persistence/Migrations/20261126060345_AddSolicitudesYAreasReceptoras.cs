using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VyTSystems.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSolicitudesYAreasReceptoras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "areas_receptoras",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    nombre = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    activa = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_areas_receptoras", x => x.id);
                    table.CheckConstraint("CK_areas_receptoras_nombre_no_vacio", "CHAR_LENGTH(TRIM(nombre)) BETWEEN 1 AND 150");
                    table.CheckConstraint("CK_areas_receptoras_orden_positivo", "orden > 0");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "folio_secuencias",
                columns: table => new
                {
                    anio = table.Column<int>(type: "int", nullable: false),
                    ultimo_numero = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folio_secuencias", x => x.anio);
                    table.CheckConstraint("CK_folio_secuencias_anio_valido", "anio BETWEEN 1 AND 9999");
                    table.CheckConstraint("CK_folio_secuencias_ultimo_numero_no_negativo", "ultimo_numero >= 0");
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "solicitudes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false),
                    folio = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    folio_normalizado = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    es_folio_automatico = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    fecha_entrega = table.Column<DateTime>(type: "date", nullable: false),
                    hora_entrega = table.Column<TimeSpan>(type: "time(0)", nullable: false),
                    area_receptora_id = table.Column<Guid>(type: "char(36)", nullable: false),
                    asunto = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    medio_entrega = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    estatus_entrega = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    tipo_solicitud = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    tipo_evidencia = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    tiene_acuse = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tipo_acuse = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "date", nullable: false),
                    creado_por_usuario_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    creado_en_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    idempotency_key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    idempotency_payload_hash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_solicitudes", x => x.id);
                    table.CheckConstraint("CK_solicitudes_acuse_coherente", "(tiene_acuse = 0 AND tipo_acuse = 'NoAplica') OR (tiene_acuse = 1 AND tipo_acuse <> 'NoAplica')");
                    table.CheckConstraint("CK_solicitudes_asunto_no_vacio", "CHAR_LENGTH(TRIM(asunto)) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_solicitudes_estatus_entrega", "estatus_entrega IN ('EnProceso', 'Entregada')");
                    table.CheckConstraint("CK_solicitudes_fechas_coherentes", "fecha_vencimiento >= fecha_entrega");
                    table.CheckConstraint("CK_solicitudes_folio_no_vacio", "CHAR_LENGTH(TRIM(folio)) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_solicitudes_folio_normalizado_no_vacio", "CHAR_LENGTH(TRIM(folio_normalizado)) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_solicitudes_idempotency_hash_longitud", "CHAR_LENGTH(idempotency_payload_hash) = 64");
                    table.CheckConstraint("CK_solicitudes_idempotency_key_no_vacio", "CHAR_LENGTH(TRIM(idempotency_key)) BETWEEN 1 AND 100");
                    table.CheckConstraint("CK_solicitudes_medio_entrega", "medio_entrega IN ('Digital', 'Impreso', 'PortalWeb', 'CorreoElectronico')");
                    table.CheckConstraint("CK_solicitudes_tipo_acuse", "tipo_acuse IN ('Sello', 'Folio', 'Electronico', 'NoAplica')");
                    table.CheckConstraint("CK_solicitudes_tipo_evidencia", "tipo_evidencia IN ('Carpeta', 'Link')");
                    table.CheckConstraint("CK_solicitudes_tipo_solicitud", "tipo_solicitud IN ('Informacion', 'Aclaracion', 'Otro')");
                    table.CheckConstraint("CK_solicitudes_usuario_no_vacio", "CHAR_LENGTH(TRIM(creado_por_usuario_id)) BETWEEN 1 AND 36");
                    table.ForeignKey(
                        name: "FK_solicitudes_areas_receptoras_area_receptora_id",
                        column: x => x.area_receptora_id,
                        principalTable: "areas_receptoras",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_solicitudes_asp_net_users_creado_por_usuario_id",
                        column: x => x.creado_por_usuario_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "areas_receptoras",
                columns: new[] { "id", "activa", "nombre", "orden" },
                values: new object[,]
                {
                    { new Guid("01950000-0000-7000-8000-000000000001"), true, "Dirección de Obras Públicas", 1 },
                    { new Guid("01950000-0000-7000-8000-000000000002"), true, "Finanzas", 2 },
                    { new Guid("01950000-0000-7000-8000-000000000003"), true, "Compras", 3 },
                    { new Guid("01950000-0000-7000-8000-000000000004"), true, "Auditoría interna", 4 },
                    { new Guid("01950000-0000-7000-8000-000000000005"), true, "Tecnologías de la Información", 5 },
                    { new Guid("01950000-0000-7000-8000-000000000006"), true, "Recursos Humanos", 6 },
                    { new Guid("01950000-0000-7000-8000-000000000007"), true, "Jurídico", 7 },
                    { new Guid("01950000-0000-7000-8000-000000000008"), true, "Comisión Estatal del Agua", 8 },
                    { new Guid("01950000-0000-7000-8000-000000000009"), true, "Instituto de Infraestructura Educativa", 9 },
                    { new Guid("01950000-0000-7000-8000-000000000010"), true, "Dirección de Servicios Públicos", 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_areas_receptoras_activa_orden",
                table: "areas_receptoras",
                columns: new[] { "activa", "orden" });

            migrationBuilder.CreateIndex(
                name: "UX_areas_receptoras_nombre",
                table: "areas_receptoras",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_area_receptora_id",
                table: "solicitudes",
                column: "area_receptora_id");

            migrationBuilder.CreateIndex(
                name: "IX_solicitudes_fecha_vencimiento",
                table: "solicitudes",
                column: "fecha_vencimiento");

            migrationBuilder.CreateIndex(
                name: "UX_solicitudes_folio_normalizado",
                table: "solicitudes",
                column: "folio_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_solicitudes_usuario_idempotency",
                table: "solicitudes",
                columns: new[] { "creado_por_usuario_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "folio_secuencias");

            migrationBuilder.DropTable(
                name: "solicitudes");

            migrationBuilder.DropTable(
                name: "areas_receptoras");
        }
    }
}
