using MediatR;
using VyTSystems.Application.Common.Results;

namespace VyTSystems.Application.Solicitudes.Crear;

/// <summary>
/// Comando CQRS para registrar una nueva solicitud en modalidad de folio manual o automático.
/// </summary>
public sealed record CrearSolicitudCommand(
    string UsuarioId,
    string? IdempotencyKey,
    bool GenerarFolioAutomatico,
    string? Folio,
    string? FechaEntrega,
    string? HoraEntrega,
    string? AreaReceptoraId,
    string? Asunto,
    string? MedioEntrega,
    string? EstatusEntrega,
    string? TipoSolicitud,
    string? TipoEvidencia,
    bool? TieneAcuse,
    string? TipoAcuse,
    string? FechaVencimiento) : IRequest<Result<CrearSolicitudResult>>;
