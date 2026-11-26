using VyTSystems.Domain.Common;

namespace VyTSystems.Domain.Entities;

/// <summary>
/// Catálogo de áreas receptoras a las que puede canalizarse una solicitud.
/// </summary>
public sealed class AreaReceptora : Entity<Guid>
{
    public string Nombre { get; private set; } = null!;
    public bool Activa { get; private set; }
    public int Orden { get; private set; }

    private AreaReceptora()
    {
    }

    private AreaReceptora(Guid id, string nombre, int orden, bool activa)
        : base(id)
    {
        Nombre = nombre;
        Orden = orden;
        Activa = activa;
    }

    /// <summary>
    /// Crea una instancia válida de <see cref="AreaReceptora"/> aplicando las invariantes del dominio.
    /// </summary>
    public static AreaReceptora Crear(string nombre, int orden = 1, bool activa = true, Guid? id = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador del área receptora no puede estar vacío.");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new DomainException("El nombre del área receptora es obligatorio.");
        }

        var nombreNormalizado = nombre.Trim();
        if (nombreNormalizado.Length > 150)
        {
            throw new DomainException("El nombre del área receptora no puede exceder 150 caracteres.");
        }

        if (orden <= 0)
        {
            throw new DomainException("El orden del área receptora debe ser mayor que cero.");
        }

        return new AreaReceptora(id ?? Guid.CreateVersion7(), nombreNormalizado, orden, activa);
    }
}
