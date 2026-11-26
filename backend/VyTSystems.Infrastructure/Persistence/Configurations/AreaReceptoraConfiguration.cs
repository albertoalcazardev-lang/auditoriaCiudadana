using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MySql.EntityFrameworkCore.Extensions;
using VyTSystems.Domain.Entities;

namespace VyTSystems.Infrastructure.Persistence.Configurations;

internal sealed class AreaReceptoraConfiguration : IEntityTypeConfiguration<AreaReceptora>
{
    public void Configure(EntityTypeBuilder<AreaReceptora> builder)
    {
        builder.ToTable("areas_receptoras", table =>
        {
            table.HasCheckConstraint(
                "CK_areas_receptoras_nombre_no_vacio",
                "CHAR_LENGTH(TRIM(nombre)) BETWEEN 1 AND 150");
            table.HasCheckConstraint(
                "CK_areas_receptoras_orden_positivo",
                "orden > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(x => x.Nombre)
            .HasColumnName("nombre")
            .HasColumnType("varchar(150)")
            .HasMaxLength(150)
            .IsRequired()
            .ForMySQLHasCollation("utf8mb4_0900_ai_ci");

        builder.Property(x => x.Activa)
            .HasColumnName("activa")
            .HasColumnType("tinyint(1)")
            .IsRequired();

        builder.Property(x => x.Orden)
            .HasColumnName("orden")
            .HasColumnType("int")
            .IsRequired();

        builder.HasIndex(x => x.Nombre)
            .IsUnique()
            .HasDatabaseName("UX_areas_receptoras_nombre");

        builder.HasIndex(x => new { x.Activa, x.Orden })
            .HasDatabaseName("IX_areas_receptoras_activa_orden");

        builder.HasData(
            AreaReceptora.Crear("Dirección de Obras Públicas", 1, true, Guid.Parse("01950000-0000-7000-8000-000000000001")),
            AreaReceptora.Crear("Finanzas", 2, true, Guid.Parse("01950000-0000-7000-8000-000000000002")),
            AreaReceptora.Crear("Compras", 3, true, Guid.Parse("01950000-0000-7000-8000-000000000003")),
            AreaReceptora.Crear("Auditoría interna", 4, true, Guid.Parse("01950000-0000-7000-8000-000000000004")),
            AreaReceptora.Crear("Tecnologías de la Información", 5, true, Guid.Parse("01950000-0000-7000-8000-000000000005")),
            AreaReceptora.Crear("Recursos Humanos", 6, true, Guid.Parse("01950000-0000-7000-8000-000000000006")),
            AreaReceptora.Crear("Jurídico", 7, true, Guid.Parse("01950000-0000-7000-8000-000000000007")),
            AreaReceptora.Crear("Comisión Estatal del Agua", 8, true, Guid.Parse("01950000-0000-7000-8000-000000000008")),
            AreaReceptora.Crear("Instituto de Infraestructura Educativa", 9, true, Guid.Parse("01950000-0000-7000-8000-000000000009")),
            AreaReceptora.Crear("Dirección de Servicios Públicos", 10, true, Guid.Parse("01950000-0000-7000-8000-000000000010")));
    }
}
