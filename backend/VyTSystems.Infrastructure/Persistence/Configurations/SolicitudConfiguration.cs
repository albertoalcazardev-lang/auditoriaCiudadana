using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.EntityFrameworkCore.Extensions;
using VyTSystems.Domain.Entities;

namespace VyTSystems.Infrastructure.Persistence.Configurations;

internal sealed class SolicitudConfiguration : IEntityTypeConfiguration<Solicitud>
{
    public void Configure(EntityTypeBuilder<Solicitud> builder)
    {
        builder.ToTable("solicitudes", table =>
        {
            table.HasCheckConstraint(
                "CK_solicitudes_folio_no_vacio",
                "CHAR_LENGTH(TRIM(folio)) BETWEEN 1 AND 100");
            table.HasCheckConstraint(
                "CK_solicitudes_folio_normalizado_no_vacio",
                "CHAR_LENGTH(TRIM(folio_normalizado)) BETWEEN 1 AND 100");
            table.HasCheckConstraint(
                "CK_solicitudes_asunto_no_vacio",
                "CHAR_LENGTH(TRIM(asunto)) BETWEEN 1 AND 100");
            table.HasCheckConstraint(
                "CK_solicitudes_idempotency_key_no_vacio",
                "CHAR_LENGTH(TRIM(idempotency_key)) BETWEEN 1 AND 100");
            table.HasCheckConstraint(
                "CK_solicitudes_idempotency_hash_longitud",
                "CHAR_LENGTH(idempotency_payload_hash) = 64");
            table.HasCheckConstraint(
                "CK_solicitudes_usuario_no_vacio",
                "CHAR_LENGTH(TRIM(creado_por_usuario_id)) BETWEEN 1 AND 36");
            table.HasCheckConstraint(
                "CK_solicitudes_fechas_coherentes",
                "fecha_vencimiento >= fecha_entrega");
            table.HasCheckConstraint(
                "CK_solicitudes_medio_entrega",
                "medio_entrega IN ('Digital', 'Impreso', 'PortalWeb', 'CorreoElectronico')");
            table.HasCheckConstraint(
                "CK_solicitudes_estatus_entrega",
                "estatus_entrega IN ('EnProceso', 'Entregada')");
            table.HasCheckConstraint(
                "CK_solicitudes_tipo_solicitud",
                "tipo_solicitud IN ('Informacion', 'Aclaracion', 'Otro')");
            table.HasCheckConstraint(
                "CK_solicitudes_tipo_evidencia",
                "tipo_evidencia IN ('Carpeta', 'Link')");
            table.HasCheckConstraint(
                "CK_solicitudes_tipo_acuse",
                "tipo_acuse IN ('Sello', 'Folio', 'Electronico', 'NoAplica')");
            table.HasCheckConstraint(
                "CK_solicitudes_acuse_coherente",
                "(tiene_acuse = 0 AND tipo_acuse = 'NoAplica') OR (tiene_acuse = 1 AND tipo_acuse <> 'NoAplica')");
            table.HasCheckConstraint(
                "CK_solicitudes_concurrency_token_formato",
                "CHAR_LENGTH(concurrency_token) = 32 AND REGEXP_LIKE(concurrency_token, '^[0-9a-f]{32}$', 'c')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(x => x.Folio)
            .HasColumnName("folio")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .ForMySQLHasCollation("utf8mb4_0900_as_cs");

        builder.Property(x => x.FolioNormalizado)
            .HasColumnName("folio_normalizado")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .ForMySQLHasCollation("utf8mb4_0900_ai_ci");

        builder.Property(x => x.EsFolioAutomatico)
            .HasColumnName("es_folio_automatico")
            .HasColumnType("tinyint(1)")
            .IsRequired();

        builder.Property(x => x.FechaEntrega)
            .HasColumnName("fecha_entrega")
            .HasColumnType("date")
            .HasConversion(
                d => d.ToDateTime(TimeOnly.MinValue),
                dt => DateOnly.FromDateTime(dt))
            .IsRequired();

        builder.Property(x => x.HoraEntrega)
            .HasColumnName("hora_entrega")
            .HasColumnType("time(0)")
            .HasConversion(
                t => t.ToTimeSpan(),
                ts => TimeOnly.FromTimeSpan(ts))
            .IsRequired();

        builder.Property(x => x.AreaReceptoraId)
            .HasColumnName("area_receptora_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(x => x.Asunto)
            .HasColumnName("asunto")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MedioEntrega)
            .HasColumnName("medio_entrega")
            .HasColumnType("varchar(30)")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.EstatusEntrega)
            .HasColumnName("estatus_entrega")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.TipoSolicitud)
            .HasColumnName("tipo_solicitud")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.TipoEvidencia)
            .HasColumnName("tipo_evidencia")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.TieneAcuse)
            .HasColumnName("tiene_acuse")
            .HasColumnType("tinyint(1)")
            .IsRequired();

        builder.Property(x => x.TipoAcuse)
            .HasColumnName("tipo_acuse")
            .HasColumnType("varchar(20)")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.FechaVencimiento)
            .HasColumnName("fecha_vencimiento")
            .HasColumnType("date")
            .HasConversion(
                d => d.ToDateTime(TimeOnly.MinValue),
                dt => DateOnly.FromDateTime(dt))
            .IsRequired();

        builder.Property(x => x.CreadoPorUsuarioId)
            .HasColumnName("creado_por_usuario_id")
            .HasColumnType("varchar(36)")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.CreadoEnUtc)
            .HasColumnName("creado_en_utc")
            .HasColumnType("datetime(6)")
            .HasConversion(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .IsRequired();

        builder.Property<string>("IdempotencyKey")
            .HasColumnName("idempotency_key")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .ForMySQLHasCollation("utf8mb4_0900_as_cs");

        builder.Property<string>("IdempotencyPayloadHash")
            .HasColumnName("idempotency_payload_hash")
            .HasColumnType("char(64)")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired()
            .ForMySQLHasCollation("utf8mb4_0900_as_cs");

        builder.Property<string>("ConcurrencyToken")
            .HasColumnName("concurrency_token")
            .HasColumnType("char(32)")
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired()
            .IsConcurrencyToken()
            .ForMySQLHasCollation("utf8mb4_0900_as_cs");

        builder.HasIndex(x => x.FolioNormalizado)
            .IsUnique()
            .HasDatabaseName("UX_solicitudes_folio_normalizado");

        builder.HasIndex(nameof(Solicitud.CreadoPorUsuarioId), "IdempotencyKey")
            .IsUnique()
            .HasDatabaseName("UX_solicitudes_usuario_idempotency");

        builder.HasIndex(x => x.AreaReceptoraId)
            .HasDatabaseName("IX_solicitudes_area_receptora_id");

        builder.HasIndex(x => x.FechaVencimiento)
            .HasDatabaseName("IX_solicitudes_fecha_vencimiento");

        builder.HasOne(x => x.AreaReceptora)
            .WithMany()
            .HasForeignKey(x => x.AreaReceptoraId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_solicitudes_areas_receptoras_area_receptora_id");

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.CreadoPorUsuarioId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_solicitudes_asp_net_users_creado_por_usuario_id");
    }
}
