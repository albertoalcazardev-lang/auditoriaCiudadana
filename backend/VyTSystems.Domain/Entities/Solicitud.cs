using System.Text.RegularExpressions;
using VyTSystems.Domain.Common;
using VyTSystems.Domain.Enums;

namespace VyTSystems.Domain.Entities;

/// <summary>
/// Entidad raíz de una solicitud de información registrada en la plataforma.
/// </summary>
public sealed partial class Solicitud : Entity<Guid>
{
    public const int MaxFolioLength = 100;
    public const int MaxAsuntoLength = 100;
    public const int MaxUsuarioIdLength = 36;

    [GeneratedRegex(@"^(?=.*[A-Za-z0-9])[A-Za-z0-9._/-]{1,100}$", RegexOptions.CultureInvariant)]
    private static partial Regex FolioRegex();

    public string Folio { get; private set; } = null!;
    public string FolioNormalizado { get; private set; } = null!;
    public bool EsFolioAutomatico { get; private set; }
    public DateOnly FechaEntrega { get; private set; }
    public TimeOnly HoraEntrega { get; private set; }
    public Guid AreaReceptoraId { get; private set; }
    public AreaReceptora? AreaReceptora { get; private set; }
    public string Asunto { get; private set; } = null!;
    public MedioEntrega MedioEntrega { get; private set; }
    public EstatusEntrega EstatusEntrega { get; private set; }
    public TipoSolicitud TipoSolicitud { get; private set; }
    public TipoEvidencia TipoEvidencia { get; private set; }
    public bool TieneAcuse { get; private set; }
    public TipoAcuse TipoAcuse { get; private set; }
    public DateOnly FechaVencimiento { get; private set; }
    public string CreadoPorUsuarioId { get; private set; } = null!;
    public DateTime CreadoEnUtc { get; private set; }

    private Solicitud()
    {
    }

    private Solicitud(
        Guid id,
        string folio,
        string folioNormalizado,
        bool esFolioAutomatico,
        DateOnly fechaEntrega,
        TimeOnly horaEntrega,
        Guid areaReceptoraId,
        string asunto,
        MedioEntrega medioEntrega,
        EstatusEntrega estatusEntrega,
        TipoSolicitud tipoSolicitud,
        TipoEvidencia tipoEvidencia,
        bool tieneAcuse,
        TipoAcuse tipoAcuse,
        DateOnly fechaVencimiento,
        string creadoPorUsuarioId,
        DateTime creadoEnUtc)
        : base(id)
    {
        Folio = folio;
        FolioNormalizado = folioNormalizado;
        EsFolioAutomatico = esFolioAutomatico;
        FechaEntrega = fechaEntrega;
        HoraEntrega = horaEntrega;
        AreaReceptoraId = areaReceptoraId;
        Asunto = asunto;
        MedioEntrega = medioEntrega;
        EstatusEntrega = estatusEntrega;
        TipoSolicitud = tipoSolicitud;
        TipoEvidencia = tipoEvidencia;
        TieneAcuse = tieneAcuse;
        TipoAcuse = tipoAcuse;
        FechaVencimiento = fechaVencimiento;
        CreadoPorUsuarioId = creadoPorUsuarioId;
        CreadoEnUtc = creadoEnUtc;
    }

    /// <summary>
    /// Crea una nueva instancia válida de <see cref="Solicitud"/>.
    /// </summary>
    public static Solicitud Crear(
        string folio,
        DateOnly fechaEntrega,
        TimeOnly horaEntrega,
        Guid areaReceptoraId,
        string asunto,
        MedioEntrega medioEntrega,
        EstatusEntrega estatusEntrega,
        TipoSolicitud tipoSolicitud,
        TipoEvidencia tipoEvidencia,
        bool tieneAcuse,
        TipoAcuse tipoAcuse,
        DateOnly fechaVencimiento,
        string creadoPorUsuarioId,
        DateTime creadoEnUtc,
        bool esFolioAutomatico = false)
    {
        if (!TryLimpiarFolio(folio, out var folioLimpio, out var errorFolio))
        {
            throw new DomainException(errorFolio!);
        }

        var (asuntoLimpio, horaNormalizada) = ValidarYPrepararDatosEditables(
            fechaEntrega,
            horaEntrega,
            areaReceptoraId,
            asunto,
            medioEntrega,
            estatusEntrega,
            tipoSolicitud,
            tipoEvidencia,
            tieneAcuse,
            tipoAcuse,
            fechaVencimiento);

        var usuarioIdLimpio = ValidarAuditoria(creadoPorUsuarioId, creadoEnUtc);

        return new Solicitud(
            Guid.CreateVersion7(),
            folioLimpio,
            folioLimpio.ToUpperInvariant(),
            esFolioAutomatico,
            fechaEntrega,
            horaNormalizada,
            areaReceptoraId,
            asuntoLimpio,
            medioEntrega,
            estatusEntrega,
            tipoSolicitud,
            tipoEvidencia,
            tieneAcuse,
            tipoAcuse,
            fechaVencimiento,
            usuarioIdLimpio,
            creadoEnUtc);
    }

    /// <summary>
    /// Actualiza de forma atómica los datos editables de la solicitud preservando su identidad, folio y auditoría de creación.
    /// </summary>
    public void ActualizarDatos(
        DateOnly fechaEntrega,
        TimeOnly horaEntrega,
        Guid areaReceptoraId,
        string asunto,
        MedioEntrega medioEntrega,
        EstatusEntrega estatusEntrega,
        TipoSolicitud tipoSolicitud,
        TipoEvidencia tipoEvidencia,
        bool tieneAcuse,
        TipoAcuse tipoAcuse,
        DateOnly fechaVencimiento)
    {
        var (asuntoLimpio, horaNormalizada) = ValidarYPrepararDatosEditables(
            fechaEntrega,
            horaEntrega,
            areaReceptoraId,
            asunto,
            medioEntrega,
            estatusEntrega,
            tipoSolicitud,
            tipoEvidencia,
            tieneAcuse,
            tipoAcuse,
            fechaVencimiento);

        if (AreaReceptora is not null && AreaReceptora.Id != areaReceptoraId)
        {
            AreaReceptora = null;
        }

        FechaEntrega = fechaEntrega;
        HoraEntrega = horaNormalizada;
        AreaReceptoraId = areaReceptoraId;
        Asunto = asuntoLimpio;
        MedioEntrega = medioEntrega;
        EstatusEntrega = estatusEntrega;
        TipoSolicitud = tipoSolicitud;
        TipoEvidencia = tipoEvidencia;
        TieneAcuse = tieneAcuse;
        TipoAcuse = tipoAcuse;
        FechaVencimiento = fechaVencimiento;
    }

    /// <summary>
    /// Calcula los días restantes no negativos hasta la fecha de vencimiento respecto al día indicado.
    /// </summary>
    public int CalcularDiasRestantes(DateOnly hoy) =>
        Math.Max(0, FechaVencimiento.DayNumber - hoy.DayNumber);

    /// <summary>
    /// Obtiene el estatus efectivo considerando si ya fue entregada o si superó la fecha de vencimiento.
    /// </summary>
    public EstatusEfectivo ObtenerEstatusEfectivo(DateOnly hoy) =>
        EstatusEntrega == EstatusEntrega.Entregada
            ? EstatusEfectivo.Entregada
            : hoy > FechaVencimiento
                ? EstatusEfectivo.Vencida
                : EstatusEfectivo.EnProceso;

    /// <summary>
    /// Determina si una cadena cumple con el formato válido de folio de dominio.
    /// </summary>
    public static bool EsFolioValido(string? folio) =>
        TryLimpiarFolio(folio, out _, out _);

    /// <summary>
    /// Valida y normaliza un folio conservando su formato alfanumérico válido en mayúsculas invariantes.
    /// </summary>
    public static string NormalizarFolio(string folio)
    {
        if (!TryLimpiarFolio(folio, out var folioLimpio, out var error))
        {
            throw new DomainException(error!);
        }

        return folioLimpio.ToUpperInvariant();
    }

    /// <summary>
    /// Formatea el folio automático con el patrón SOL-{YYYY}-{CCC}.
    /// </summary>
    public static string FormatearFolioAutomatico(int anio, int consecutivo)
    {
        if (anio is < 1 or > 9999)
        {
            throw new DomainException("El año del folio automático debe estar entre 1 y 9999.");
        }

        if (consecutivo < 1)
        {
            throw new DomainException("El consecutivo del folio automático debe ser mayor que cero.");
        }

        return FormattableString.Invariant($"SOL-{anio:D4}-{consecutivo:D3}");
    }

    private static bool TryLimpiarFolio(string? folio, out string folioLimpio, out string? error)
    {
        folioLimpio = string.Empty;

        if (string.IsNullOrWhiteSpace(folio) || folio.Any(char.IsControl))
        {
            error = "El folio es obligatorio y no puede contener caracteres de control.";
            return false;
        }

        var recortado = folio.Trim();
        if (recortado.Length > MaxFolioLength || !FolioRegex().IsMatch(recortado))
        {
            error =
                "El folio debe tener entre 1 y 100 caracteres válidos (letras, números, '.', '_', '/', '-') e incluir al menos un carácter alfanumérico.";
            return false;
        }

        folioLimpio = recortado;
        error = null;
        return true;
    }

    private static (string AsuntoLimpio, TimeOnly HoraNormalizada) ValidarYPrepararDatosEditables(
        DateOnly fechaEntrega,
        TimeOnly horaEntrega,
        Guid areaReceptoraId,
        string asunto,
        MedioEntrega medioEntrega,
        EstatusEntrega estatusEntrega,
        TipoSolicitud tipoSolicitud,
        TipoEvidencia tipoEvidencia,
        bool tieneAcuse,
        TipoAcuse tipoAcuse,
        DateOnly fechaVencimiento)
    {
        ValidarPlazo(fechaEntrega, fechaVencimiento);
        ValidarAreaReceptora(areaReceptoraId);
        var asuntoLimpio = ValidarYLimpiarAsunto(asunto);
        ValidarEnums(medioEntrega, estatusEntrega, tipoSolicitud, tipoEvidencia, tipoAcuse);
        ValidarAcuse(tieneAcuse, tipoAcuse);

        return (asuntoLimpio, new TimeOnly(horaEntrega.Hour, horaEntrega.Minute));
    }

    private static void ValidarAreaReceptora(Guid areaReceptoraId)
    {
        if (areaReceptoraId == Guid.Empty)
        {
            throw new DomainException("El área receptora es obligatoria.");
        }
    }

    private static void ValidarPlazo(DateOnly fechaEntrega, DateOnly fechaVencimiento)
    {
        if (fechaEntrega == default)
        {
            throw new DomainException("La fecha de entrega es obligatoria.");
        }

        if (fechaVencimiento == default)
        {
            throw new DomainException("La fecha de vencimiento es obligatoria.");
        }

        if (fechaVencimiento < fechaEntrega)
        {
            throw new DomainException("La fecha de vencimiento no puede ser anterior a la fecha de entrega.");
        }
    }

    private static string ValidarYLimpiarAsunto(string asunto)
    {
        if (string.IsNullOrWhiteSpace(asunto) || asunto.Any(char.IsControl))
        {
            throw new DomainException("El asunto es obligatorio y no puede contener caracteres de control.");
        }

        var asuntoLimpio = asunto.Trim();
        if (asuntoLimpio.Length > MaxAsuntoLength)
        {
            throw new DomainException("El asunto no puede exceder 100 caracteres.");
        }

        return asuntoLimpio;
    }

    private static void ValidarAcuse(bool tieneAcuse, TipoAcuse tipoAcuse)
    {
        if (!tieneAcuse && tipoAcuse != TipoAcuse.NoAplica)
        {
            throw new DomainException("Cuando no existe acuse, el tipo de acuse debe ser NoAplica.");
        }

        if (tieneAcuse && tipoAcuse == TipoAcuse.NoAplica)
        {
            throw new DomainException("Cuando existe acuse, el tipo de acuse no puede ser NoAplica.");
        }
    }

    private static string ValidarAuditoria(string creadoPorUsuarioId, DateTime creadoEnUtc)
    {
        if (string.IsNullOrWhiteSpace(creadoPorUsuarioId) || creadoPorUsuarioId.Any(char.IsControl))
        {
            throw new DomainException("El usuario creador es obligatorio y no puede contener caracteres de control.");
        }

        var usuarioIdLimpio = creadoPorUsuarioId.Trim();
        if (usuarioIdLimpio.Length > MaxUsuarioIdLength)
        {
            throw new DomainException("El identificador del usuario creador excede la longitud máxima permitida.");
        }

        if (creadoEnUtc == default || creadoEnUtc.Kind != DateTimeKind.Utc)
        {
            throw new DomainException("La fecha de creación debe especificarse en formato UTC válido.");
        }

        return usuarioIdLimpio;
    }

    private static void ValidarEnums(
        MedioEntrega medioEntrega,
        EstatusEntrega estatusEntrega,
        TipoSolicitud tipoSolicitud,
        TipoEvidencia tipoEvidencia,
        TipoAcuse tipoAcuse)
    {
        if (!Enum.IsDefined(medioEntrega))
        {
            throw new DomainException("El medio de entrega no es válido.");
        }

        if (!Enum.IsDefined(estatusEntrega))
        {
            throw new DomainException("El estatus de entrega no es válido.");
        }

        if (!Enum.IsDefined(tipoSolicitud))
        {
            throw new DomainException("El tipo de solicitud no es válido.");
        }

        if (!Enum.IsDefined(tipoEvidencia))
        {
            throw new DomainException("El tipo de evidencia no es válido.");
        }

        if (!Enum.IsDefined(tipoAcuse))
        {
            throw new DomainException("El tipo de acuse no es válido.");
        }
    }
}
