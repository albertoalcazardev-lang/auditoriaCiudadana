using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.EntityFrameworkCore.Extensions;
using VyTSystems.Infrastructure.Persistence.Entities;

namespace VyTSystems.Infrastructure.Persistence.Configurations;

internal sealed class SolicitudIdempotenciaEliminadaConfiguration : IEntityTypeConfiguration<SolicitudIdempotenciaEliminada>
{
    public void Configure(EntityTypeBuilder<SolicitudIdempotenciaEliminada> builder)
    {
        builder.ToTable("solicitud_idempotencias_eliminadas", table =>
        {
            table.HasCheckConstraint(
                "CK_solicitud_idempotencias_eliminadas_usuario_no_vacio",
                "CHAR_LENGTH(TRIM(creado_por_usuario_id)) BETWEEN 1 AND 36");
            table.HasCheckConstraint(
                "CK_solicitud_idempotencias_eliminadas_key_no_vacio",
                "CHAR_LENGTH(TRIM(idempotency_key)) BETWEEN 1 AND 100");
            table.HasCheckConstraint(
                "CK_solicitud_idempotencias_eliminadas_hash_longitud",
                "CHAR_LENGTH(idempotency_payload_hash) = 64");
        });

        builder.HasKey(x => new { x.CreadoPorUsuarioId, x.IdempotencyKey });

        builder.Property(x => x.CreadoPorUsuarioId)
            .HasColumnName("creado_por_usuario_id")
            .HasColumnType("varchar(36)")
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .IsRequired()
            .ForMySQLHasCollation("utf8mb4_0900_as_cs");

        builder.Property(x => x.IdempotencyPayloadHash)
            .HasColumnName("idempotency_payload_hash")
            .HasColumnType("char(64)")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired()
            .ForMySQLHasCollation("utf8mb4_0900_as_cs");

        builder.Property(x => x.SolicitudId)
            .HasColumnName("solicitud_id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.EliminadoEnUtc)
            .HasColumnName("eliminado_en_utc")
            .HasColumnType("datetime(6)")
            .HasConversion(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .IsRequired();

        builder.HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(x => x.CreadoPorUsuarioId)
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_solicitud_idempotencias_eliminadas_asp_net_users_usuario_id");
    }
}
