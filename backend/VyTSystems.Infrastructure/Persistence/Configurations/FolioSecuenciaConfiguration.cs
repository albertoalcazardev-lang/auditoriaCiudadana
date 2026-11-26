using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VyTSystems.Infrastructure.Persistence.Entities;

namespace VyTSystems.Infrastructure.Persistence.Configurations;

internal sealed class FolioSecuenciaConfiguration : IEntityTypeConfiguration<FolioSecuencia>
{
    public void Configure(EntityTypeBuilder<FolioSecuencia> builder)
    {
        builder.ToTable("folio_secuencias", table =>
        {
            table.HasCheckConstraint(
                "CK_folio_secuencias_anio_valido",
                "anio BETWEEN 1 AND 9999");
            table.HasCheckConstraint(
                "CK_folio_secuencias_ultimo_numero_no_negativo",
                "ultimo_numero >= 0");
        });

        builder.HasKey(x => x.Anio);

        builder.Property(x => x.Anio)
            .HasColumnName("anio")
            .HasColumnType("int")
            .ValueGeneratedNever();

        builder.Property(x => x.UltimoNumero)
            .HasColumnName("ultimo_numero")
            .HasColumnType("int")
            .IsRequired();
    }
}
