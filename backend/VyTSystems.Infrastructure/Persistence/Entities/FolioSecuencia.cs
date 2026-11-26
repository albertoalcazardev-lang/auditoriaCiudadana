namespace VyTSystems.Infrastructure.Persistence.Entities;

/// <summary>
/// Registro de secuencia anual para asignación atómica de folios automáticos.
/// </summary>
internal sealed class FolioSecuencia
{
    public int Anio { get; set; }
    public int UltimoNumero { get; set; }
}
