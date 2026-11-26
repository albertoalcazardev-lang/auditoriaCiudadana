using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using VyTSystems.Application.Solicitudes.Common;
using VyTSystems.Domain.Entities;

namespace VyTSystems.Application.Solicitudes.Listar;

/// <summary>
/// Validador de parámetros de consulta para <see cref="ListarSolicitudesQuery"/> sin acceso a base de datos.
/// </summary>
public sealed partial class ListarSolicitudesValidator : AbstractValidator<ListarSolicitudesQuery>
{
    private static readonly HashSet<string> ValoresDiasPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "todos",
        "0",
        "vencidas",
        "1-3",
        "4-7",
        "1-7",
        "8+",
        "custom",
        "personalizado"
    };

    private static readonly HashSet<string> ValoresSortPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "vencimiento",
        "folio",
        "entrega",
        "dias"
    };

    private static readonly HashSet<string> ValoresDirPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "asc",
        "desc"
    };

    [GeneratedRegex(@"^\d{1,2}/\d{1,2}/\d{4}$", RegexOptions.CultureInvariant)]
    internal static partial Regex CandidatoFechaConBarrasRegex();

    public ListarSolicitudesValidator()
    {
        RuleFor(x => x.Q).Custom((raw, context) =>
        {
            if (raw is null)
            {
                return;
            }

            if (raw.Any(char.IsControl))
            {
                context.AddFailure("q", "El parámetro de búsqueda no puede contener caracteres de control ni enviarse duplicado.");
                return;
            }

            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                return;
            }

            if (trimmed.Length > Solicitud.MaxFolioLength)
            {
                context.AddFailure("q", $"El texto de búsqueda no puede exceder {Solicitud.MaxFolioLength} caracteres.");
                return;
            }

            if (CandidatoFechaConBarrasRegex().IsMatch(trimmed))
            {
                if (!DateOnly.TryParseExact(
                        trimmed,
                        "dd/MM/yyyy",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var fecha) ||
                    fecha.Year < 1900)
                {
                    context.AddFailure(
                        "q",
                        "La fecha ingresada no es válida. Usa el formato exacto dd/mm/aaaa con una fecha real del calendario.");
                }
            }
        });

        When(x => x.EntD is not null, () =>
        {
            RuleFor(x => x.EntD)
                .Must(raw => SolicitudInputParser.TryParseFecha(raw, out _))
                .WithMessage("La fecha inicial de entrega debe tener el formato válido yyyy-MM-dd.")
                .OverridePropertyName("ent_d");
        });

        When(x => x.EntH is not null, () =>
        {
            RuleFor(x => x.EntH)
                .Cascade(CascadeMode.Stop)
                .Must(raw => SolicitudInputParser.TryParseFecha(raw, out _))
                .WithMessage("La fecha final de entrega debe tener el formato válido yyyy-MM-dd.")
                .Must((instance, raw) =>
                {
                    if (!SolicitudInputParser.TryParseFecha(instance.EntD, out var desde) ||
                        !SolicitudInputParser.TryParseFecha(raw, out var hasta))
                    {
                        return true;
                    }

                    return hasta >= desde;
                })
                .WithMessage("La fecha final de entrega no puede ser anterior a la fecha inicial.")
                .OverridePropertyName("ent_h");
        });

        When(x => x.VenD is not null, () =>
        {
            RuleFor(x => x.VenD)
                .Must(raw => SolicitudInputParser.TryParseFecha(raw, out _))
                .WithMessage("La fecha inicial de vencimiento debe tener el formato válido yyyy-MM-dd.")
                .OverridePropertyName("ven_d");
        });

        When(x => x.VenH is not null, () =>
        {
            RuleFor(x => x.VenH)
                .Cascade(CascadeMode.Stop)
                .Must(raw => SolicitudInputParser.TryParseFecha(raw, out _))
                .WithMessage("La fecha final de vencimiento debe tener el formato válido yyyy-MM-dd.")
                .Must((instance, raw) =>
                {
                    if (!SolicitudInputParser.TryParseFecha(instance.VenD, out var desde) ||
                        !SolicitudInputParser.TryParseFecha(raw, out var hasta))
                    {
                        return true;
                    }

                    return hasta >= desde;
                })
                .WithMessage("La fecha final de vencimiento no puede ser anterior a la fecha inicial.")
                .OverridePropertyName("ven_h");
        });

        RuleFor(x => x).Custom((instance, context) =>
        {
            var esCustom = false;

            if (instance.Dias is not null)
            {
                if (string.IsNullOrWhiteSpace(instance.Dias) || instance.Dias.Any(char.IsControl))
                {
                    context.AddFailure("dias", "Selecciona un filtro de días restantes válido.");
                }
                else
                {
                    var diasTrim = instance.Dias.Trim();
                    if (!ValoresDiasPermitidos.Contains(diasTrim))
                    {
                        context.AddFailure("dias", "Selecciona un filtro de días restantes válido.");
                    }
                    else
                    {
                        esCustom = EsFiltroDiasPersonalizado(diasTrim);
                    }
                }
            }

            if (!esCustom)
            {
                if (instance.DiasMin is not null)
                {
                    context.AddFailure("dias_min", "El límite mínimo de días solo se permite cuando el filtro 'dias' es personalizado.");
                }

                if (instance.DiasMax is not null)
                {
                    context.AddFailure("dias_max", "El límite máximo de días solo se permite cuando el filtro 'dias' es personalizado.");
                }

                return;
            }

            if (instance.DiasMin is null && instance.DiasMax is null)
            {
                context.AddFailure("dias", "Indica al menos el mínimo o el máximo de días para el rango personalizado.");
                return;
            }

            int? minParsed = null;
            int? maxParsed = null;
            var limitesValidos = true;

            if (instance.DiasMin is not null)
            {
                if (!TryParseDiasPersonalizados(instance.DiasMin, out var minVal))
                {
                    context.AddFailure("dias_min", "El mínimo de días restantes debe ser un entero entre 0 y 999.");
                    limitesValidos = false;
                }
                else
                {
                    minParsed = minVal;
                }
            }

            if (instance.DiasMax is not null)
            {
                if (!TryParseDiasPersonalizados(instance.DiasMax, out var maxVal))
                {
                    context.AddFailure("dias_max", "El máximo de días restantes debe ser un entero entre 0 y 999.");
                    limitesValidos = false;
                }
                else
                {
                    maxParsed = maxVal;
                }
            }

            if (limitesValidos && (minParsed ?? 0) > (maxParsed ?? 999))
            {
                context.AddFailure("dias_max", "El máximo de días restantes no puede ser menor que el mínimo.");
            }
        });

        When(x => x.Sort is not null, () =>
        {
            RuleFor(x => x.Sort)
                .Must(EsSortValido)
                .WithMessage("La columna de ordenamiento debe ser 'vencimiento', 'folio', 'entrega' o 'dias'.")
                .OverridePropertyName("sort");
        });

        When(x => x.Dir is not null, () =>
        {
            RuleFor(x => x.Dir)
                .Must(EsDirValido)
                .WithMessage("La dirección de ordenamiento debe ser 'asc' o 'desc'.")
                .OverridePropertyName("dir");
        });

        When(x => x.Page is not null, () =>
        {
            RuleFor(x => x.Page)
                .Must(raw => TryParseEnteroPositivo(raw, out _))
                .WithMessage("El número de página debe ser un entero mayor o igual a 1.")
                .OverridePropertyName("page");
        });

        When(x => x.Size is not null, () =>
        {
            RuleFor(x => x.Size)
                .Must(raw => TryParseTamanoPagina(raw, out _))
                .WithMessage("El tamaño de página debe ser 10, 25 o 50.")
                .OverridePropertyName("size");
        });
    }

    internal static bool EsFiltroDiasPersonalizado(string diasTrimmed) =>
        string.Equals(diasTrimmed, "custom", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(diasTrimmed, "personalizado", StringComparison.OrdinalIgnoreCase);

    internal static bool TryParseDiasPersonalizados(string? raw, out int valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(raw) || raw.Any(char.IsControl))
        {
            return false;
        }

        return int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out valor) &&
               valor is >= 0 and <= 999;
    }

    internal static bool TryParseEnteroPositivo(string? raw, out int valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(raw) || raw.Any(char.IsControl))
        {
            return false;
        }

        return int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out valor) &&
               valor >= 1;
    }

    internal static bool TryParseTamanoPagina(string? raw, out int valor)
    {
        if (!TryParseEnteroPositivo(raw, out valor))
        {
            return false;
        }

        return valor is 10 or 25 or 50;
    }

    private static bool EsSortValido(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) &&
        !raw.Any(char.IsControl) &&
        ValoresSortPermitidos.Contains(raw.Trim());

    private static bool EsDirValido(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) &&
        !raw.Any(char.IsControl) &&
        ValoresDirPermitidos.Contains(raw.Trim());
}
