namespace VyTSystems.API.Contracts.Solicitudes;

public sealed record CrearSolicitudResponse(
    string Id,
    string Folio,
    DateOnly FechaVencimiento,
    int DiasRestantes,
    string Estatus);
